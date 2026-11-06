using System;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Domain.Api.Base;
using Domain.Api.Interfaces;
using Infrastructure.Networking;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Networking;
using VContainer;

/// <summary>
/// ApiService is a high-performance, lightweight API Service optimized for Unity.
/// Uses UniTask for zero-allocation async/await and UnityWebRequest for native networking.
/// </summary>
public class GameApiService : BaseApiService, IGameApiService
{
    private const int DefaultTimeout = 30;

    /// <summary>Network-error resends allowed after the first attempt (user is prompted before each).</summary>
    public const int MaxNetworkRetries = 3;

    private const string InvalidResponseMessage = "Invalid server response";

    // Failed = the session is gone (cleared or superseded); ServerError = server/portal problem, session kept.
    private enum RefreshOutcome { Succeeded, Failed, ServerError, NetworkError }

    // Non-null while a token refresh is in flight; every concurrent caller awaits the same outcome.
    private UniTaskCompletionSource<RefreshOutcome> _refresh;

    // Non-null while the "no connection" prompt is showing; concurrent failures share one prompt.
    private UniTaskCompletionSource<bool> _networkPrompt;

    // Dependencies (Inject lewat Constructor)
    private readonly IHttpClient _httpClient;
    private readonly IAuthStore _authStore;
    private readonly IApiConfig _config;
    private readonly IApiErrorHandler _apiErrorHandler;
    private readonly ApiEndpoints _endpoints;

    [Inject]
    public GameApiService(IHttpClient httpClient, IAuthStore authStore,
            IApiConfig config, IApiErrorHandler apiErrorHandler, ApiEndpoints endpoints) : base()
    {
        _httpClient = httpClient;
        _authStore = authStore;
        _config = config;
        _apiErrorHandler = apiErrorHandler;
        _endpoints = endpoints;
    }

    #region Public Methods
    public async UniTask<ApiResponseDto<T>> GetAsync<T>(string url, CancellationToken ct = default)
    {
        return await SendRequestWithInterceptAsync<T>(() =>
        {
            var req = UnityWebRequest.Get(url);
            req.downloadHandler = new DownloadHandlerBuffer();
            return req;
        }, ct);
    }

    public async UniTask<ApiResponseDto<T>> PostAsync<T>(string url, object data, CancellationToken ct = default)
    {
        return await SendRequestWithInterceptAsync<T>(() =>
        {
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPOST);
            SetupJsonBody(req, data);
            return req;
        }, ct);
    }

    /// <summary>
    /// PUT Request: Updates an existing resource.
    /// </summary>
    public async UniTask<ApiResponseDto<T>> PutAsync<T>(string url, object data, CancellationToken ct = default)
    {
        return await SendRequestWithInterceptAsync<T>(() =>
        {
            var req = new UnityWebRequest(url, UnityWebRequest.kHttpVerbPUT);
            SetupJsonBody(req, data);
            return req;
        }, ct);
    }

    /// <summary>
    /// DELETE Request. True only when the call succeeded (an empty 2xx body counts as success).
    /// </summary>
    public async UniTask<bool> DeleteAsync(string url, CancellationToken ct = default)
    {
        var result = await SendRequestWithInterceptAsync<string>(() => UnityWebRequest.Delete(url), ct);
        return result.IsSuccess;
    }

    #endregion

    #region Core Logic
    /// <summary>
    /// Request handler with interceptor for token refresh and a bounded network-retry loop.
    /// Takes a factory function to create the request, allowing resend after refresh or retry.
    /// </summary>
    private async UniTask<ApiResponseDto<T>> SendRequestWithInterceptAsync<T>(
        Func<UnityWebRequest> requestFactory, CancellationToken ct)
    {
        try
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    return await SendOnceAsync<T>(requestFactory, ct);
                }
                catch (NetworkException)
                {
                    LoggerService.Warning("[API] Network connection lost.");

                    // Out of retries, or user chose not to retry: give up with a handled error.
                    if (attempt >= MaxNetworkRetries || !await PromptNetworkRetryAsync())
                        return ApiResponseDto<T>.CreateError("NETWORK_ERROR", 0, true);

                    LoggerService.LogDebug("[API] User chose to retry. Resending request...");
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Jika request dibatalkan secara sengaja (misal pindah scene)
            return ApiResponseDto<T>.CreateError("Canceled", 0, true);
        }
        catch (Exception ex)
        {
            LoggerService.Exception(ex, "[API] Unexpected exception");
            // Kembalikan DTO error agar Presenter tidak crash & tahu UI sudah dihandle
            return ApiResponseDto<T>.CreateError(ex.Message, 0, true);
        }
    }

    /// <summary>Shows the retry prompt once; callers failing while it is open await the same answer.</summary>
    private async UniTask<bool> PromptNetworkRetryAsync()
    {
        if (_networkPrompt != null) return await _networkPrompt.Task;

        var tcs = _networkPrompt = new UniTaskCompletionSource<bool>();
        bool retry = false;
        try
        {
            retry = await _apiErrorHandler.ShowNetworkErrorPromptAsync();
        }
        finally
        {
            _networkPrompt = null;
            tcs.TrySetResult(retry);
        }

        return retry;
    }

    /// <summary>One send, with token refresh + resend when the access token has expired.</summary>
    /// <exception cref="NetworkException">No response received (also from the refresh call).</exception>
    private async UniTask<ApiResponseDto<T>> SendOnceAsync<T>(Func<UnityWebRequest> requestFactory, CancellationToken ct)
    {
        // A refresh is in flight: wait so we send with the fresh token (prevents request storms).
        if (_refresh != null)
        {
            var pending = await _refresh.Task.AttachExternalCancellation(ct);
            if (pending == RefreshOutcome.Failed)
                return ApiResponseDto<T>.CreateError("Session expired", 401, true);
            if (pending == RefreshOutcome.ServerError)
                return ApiResponseDto<T>.CreateError("Server unavailable", 503, true);
        }

        var (response, result) = await SendParsedAsync<T>(requestFactory, ct);

        // --- HANDLE ACCESS TOKEN EXPIRED ---
        if (_authStore.IsLoggedIn && IsAccessTokenExpired(response))
        {
            if (!await RefreshTokenAsync(ct))
            {
                result.SetHandledByGlobalUI(true);
                return result;
            }

            // Token is now updated: resend with a fresh request
            var (retryResponse, retryResult) = await SendParsedAsync<T>(requestFactory, ct);

            // Check for permanent expiration on retry
            if (IsSessionPermanentlyExpired(retryResponse) || IsAccessTokenExpired(retryResponse))
            {
                LoggerService.Warning("[API] Session invalid after retry. Logging out.");
                _authStore.ClearSession();
                retryResult.SetHandledByGlobalUI(true); // Tandai agar Presenter diam
                return retryResult;
            }

            return HandleGlobalErrors(retryResult, retryResponse);
        }

        return HandleGlobalErrors(result, response);
    }

    private async UniTask<(HttpResponse response, ApiResponseDto<T> result)> SendParsedAsync<T>(
        Func<UnityWebRequest> requestFactory, CancellationToken ct)
    {
        using var request = requestFactory();
        SetupRequestHeaders(request);

        LoggerService.LogDebug($"[API] Sending Request: {request.method} {LogRedactor.Redact(request.url)}");
        var response = await _httpClient.SendAsync(request, ct);
        LoggerService.LogDebug($"[API] Response ({response.Code}): {LogRedactor.Redact(response.Body)}");

        return (response, Parse<T>(response));
    }

    /// <summary>
    /// Body is optional/untrusted (HTML error pages, empty): the HTTP code is always kept.
    /// An empty body is fine; a non-empty body that is not a usable envelope becomes an error.
    /// </summary>
    private static ApiResponseDto<T> Parse<T>(HttpResponse response)
    {
        ApiResponseDto<T> dto = null;
        if (!string.IsNullOrWhiteSpace(response.Body))
        {
            try { dto = JsonConvert.DeserializeObject<ApiResponseDto<T>>(response.Body); }
            catch (JsonException) { dto = Recover<T>(response.Body); }
        }

        dto ??= new ApiResponseDto<T>();
        dto.Code = (int)response.Code;
        return dto;
    }

    /// <summary>
    /// Body did not fit the envelope for T (HTML page, or `data` of another shape).
    /// Keep the backend's own error status/message when the envelope is readable; never report success.
    /// </summary>
    private static ApiResponseDto<T> Recover<T>(string body)
    {
        bool status = false;
        string message = null;
        try
        {
            var envelope = JsonConvert.DeserializeObject<ApiResponseDto<JToken>>(body);
            status = envelope.Success;
            message = envelope?.Message;
        }
        catch (JsonException) { /* not JSON at all */ }

        bool backendError = !status;
        return new ApiResponseDto<T>
        {
            Success = backendError ? status : false,
            Message = backendError && !string.IsNullOrEmpty(message) ? message : InvalidResponseMessage
        };
    }

    // --- HANDLE GLOBAL UI (Maintenance / Force Update / Server Error) ---
    private ApiResponseDto<T> HandleGlobalErrors<T>(ApiResponseDto<T> result, HttpResponse response)
    {
        if (result.IsGlobalError)
        {
            HandleInternalApiErrors(result.Code);
            result.SetHandledByGlobalUI(true);
        }
        else if (response.Code >= 400)
        {
            // Logging untuk error non-global (400 Bad Request, dll)
            LoggerService.Warning($"[API] Protocol Error ({result.Code}): {LogRedactor.Redact(response.Body)}");
        }

        return result;
    }

    /// <summary>
    /// Logika khusus Game api: Maintenance, Update, Server Error
    /// </summary>
    private void HandleInternalApiErrors(long responseCode)
    {
        switch (responseCode)
        {
            case 426: _apiErrorHandler.ShowUpdateRequired(); break;
            case 503: _apiErrorHandler.ShowMaintenance(); break;
            case 502:
            case 504: _apiErrorHandler.ShowServerError(); break;
        }
    }
    #endregion

    #region Helpers & Token Logic

    /// <summary>
    /// Single-flight refresh: the first caller runs it, concurrent callers await the same outcome.
    /// Returns true when the new access token is stored.
    /// </summary>
    /// <exception cref="NetworkException">No connection during refresh (session is NOT cleared).</exception>
    private async UniTask<bool> RefreshTokenAsync(CancellationToken ct)
    {
        if (_refresh != null)
            return Unwrap(await _refresh.Task.AttachExternalCancellation(ct));

        var tcs = _refresh = new UniTaskCompletionSource<RefreshOutcome>();
        var outcome = RefreshOutcome.Failed;
        try
        {
            outcome = await ExecuteRefreshAsync();
        }
        catch (NetworkException)
        {
            outcome = RefreshOutcome.NetworkError;
        }
        finally
        {
            _refresh = null;
            tcs.TrySetResult(outcome);
        }

        return Unwrap(outcome);
    }

    private static bool Unwrap(RefreshOutcome outcome)
    {
        if (outcome == RefreshOutcome.NetworkError) throw new NetworkException("NETWORK_ERROR");
        return outcome == RefreshOutcome.Succeeded;
    }

    /// <summary>
    /// Direct request that bypasses the interceptor (avoids infinite recursion).
    /// Not tied to any caller's CancellationToken: it finishes for all waiting callers.
    /// </summary>
    private async UniTask<RefreshOutcome> ExecuteRefreshAsync()
    {
        try
        {
            LoggerService.LogDebug("[API] Access token expired. Attempting refresh...");

            string refreshToken = _authStore.Session.RefreshToken;
            if (!_authStore.IsLoggedIn || string.IsNullOrEmpty(refreshToken))
            {
                LoggerService.Warning("[API] No refresh token available. Clearing session.");
                _authStore.ClearSession();
                return RefreshOutcome.Failed;
            }

            using var req = new UnityWebRequest($"{_config.API_BASE_URL}{_endpoints.Refresh}", UnityWebRequest.kHttpVerbPOST);
            SetupJsonBody(req, new { refreshToken });
            SetupRequestHeaders(req);

            var response = await _httpClient.SendAsync(req, CancellationToken.None);
            var dto = Parse<RefreshTokenDto>(response);

            if (response.Code >= 200 && response.Code < 300 && dto.IsSuccess &&
                !string.IsNullOrEmpty(dto.Data?.AccessToken))
            {
                // Logout / account switch while the request was in flight: never resurrect the old session.
                if (!_authStore.IsLoggedIn || _authStore.Session.RefreshToken != refreshToken)
                {
                    LoggerService.Warning("[API] Session changed during token refresh. Discarding the new token.");
                    return RefreshOutcome.Failed;
                }

                _authStore.SetSession(dto.Data.AccessToken, refreshToken);
                LoggerService.LogDebug("[API] Token refresh successful.");
                return RefreshOutcome.Succeeded;
            }

            // Only a rejected refresh token ends the session. Server trouble (5xx, 426, captive
            // portal, unreadable body) keeps it so the user is not logged out by a deploy.
            bool rejected = response.Code is 400 or 401 or 403 or 440 ||
                            IsSessionPermanentlyExpired(response) ||
                            (response.Code >= 200 && response.Code < 300 && !dto.IsSuccess &&
                             dto.Message != InvalidResponseMessage);
            if (rejected)
            {
                LoggerService.Warning($"[API] Refresh token invalid/expired (Code: {response.Code}). Logging out.");
                _authStore.ClearSession();
                return RefreshOutcome.Failed;
            }

            LoggerService.Warning($"[API] Token refresh failed, session kept (Code: {response.Code}).");
            if (dto.IsGlobalError) HandleInternalApiErrors(dto.Code);
            return RefreshOutcome.ServerError;
        }
        catch (NetworkException)
        {
            throw; // connectivity problem, not an invalid session
        }
        catch (Exception e)
        {
            LoggerService.Exception(e, "Exception during token refresh");
            return RefreshOutcome.ServerError;
        }
    }

    /// <summary>
    /// Sets up all required headers for the request.
    /// </summary>
    private void SetupRequestHeaders(UnityWebRequest request)
    {
        request.timeout = DefaultTimeout;

        // Add Authorization header if bearer token is configured
        string bearerToken = _config.API_BEARER;
        if (!string.IsNullOrEmpty(bearerToken))
            request.SetRequestHeader("Authorization", $"Bearer {bearerToken}");

        // Add access token and refresh token if the user is authenticated
        if (_authStore.IsLoggedIn)
        {
            string accessToken = _authStore.Session.AccessToken;
            if (!string.IsNullOrEmpty(accessToken))
                request.SetRequestHeader("x-access-token", accessToken);

            string refreshToken = _authStore.Session.RefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
                request.SetRequestHeader("x-refresh-token", refreshToken);
        }
        else
        {
            // Fallback to the persisted token if the user is not authenticated (cold start)
            string refreshToken = _authStore.StoredRefreshToken;
            if (!string.IsNullOrEmpty(refreshToken))
                request.SetRequestHeader("x-refresh-token", refreshToken);
        }

        // Add API Version header
        string apiVersion = _config.API_VERSION;
        if (!string.IsNullOrEmpty(apiVersion))
            request.SetRequestHeader("x-app-version", apiVersion);

        // Add Device Platform header
        string devicePlatform = _config.DEVICE_PLATFORM;
        if (!string.IsNullOrEmpty(devicePlatform))
            request.SetRequestHeader("x-device-platform", devicePlatform);

        // Add device-specific headers
        try
        {
            request.SetRequestHeader("x-device-id", SystemInfo.deviceUniqueIdentifier ?? "");
            request.SetRequestHeader("x-device-type", "game");
            request.SetRequestHeader("x-device-name", Application.isEditor ? "editor" : "game");
        }
        catch (Exception ex)
        {
            // Be defensive: SystemInfo access on some platforms might throw in editor tooling
            LoggerService.Warning($"[API] Failed to set device headers: {ex.Message}");
        }
    }

    private void SetupJsonBody(UnityWebRequest req, object data)
    {
        if (data == null)
        {
            // Tetap set DownloadHandler agar bisa menerima respon walau tanpa upload body
            req.downloadHandler = new DownloadHandlerBuffer();
            return;
        }

        string json = JsonConvert.SerializeObject(data);

        if (string.IsNullOrEmpty(json) || json == "null")
        {
            req.downloadHandler = new DownloadHandlerBuffer();
            return;
        }

        byte[] body = Encoding.UTF8.GetBytes(json);

        // Log JSON string for debugging, be careful not to log sensitive tokens
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        LoggerService.LogDebug($"[API] Request Body JSON: {LogRedactor.Redact(json)}");
#endif
        req.uploadHandler = new UploadHandlerRaw(body) { contentType = "application/json" };
        req.downloadHandler = new DownloadHandlerBuffer();
    }

    // --- Helper Methods for Performance ---
    private static bool IsAccessTokenExpired(HttpResponse response)
    {
        // Triggered by specific backend message or standard 401
        if (response.Code == 401) return true;
        return (response.Body ?? string.Empty).Contains("Access token expired.");
    }

    private static bool IsSessionPermanentlyExpired(HttpResponse response)
    {
        // Specific 440 code or "Token expired." message
        if (response.Code == 440) return true;
        return (response.Body ?? string.Empty).Contains("Token expired.");
    }

    #endregion
}
