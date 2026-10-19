using Cysharp.Threading.Tasks;
using System.Threading;

/// <summary>
/// High-performance, modular User API service.
/// Provides clean, reusable methods for user-related API operations.
/// Uses UniTask for zero-allocation async operations.
/// </summary>
public class UserApiService<TUser> : IUserApiService<TUser> where TUser : UserData
{
    // Dependencies (Inject lewat Constructor)
    private readonly IGameApiService _apiService;
    private readonly IApiConfig _config;
    private readonly ApiEndpoints _endpoints;

    // VContainer akan otomatis mengisi ini
    public UserApiService(IGameApiService apiService, IApiConfig config, ApiEndpoints endpoints)
    {
        _apiService = apiService;
        _config = config;
        _endpoints = endpoints;
    }

    /// <summary>
    /// Gets the current user from the API.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>User data if successful</returns>
    public async UniTask<ApiResponseDto<TUser>> GetUserAsync(
        CancellationToken ct = default)
    {
        string url = $"{_config.API_BASE_URL}{_endpoints.User}";
        return await _apiService.GetAsync<TUser>(url, ct);
    }

    public async UniTask<ApiResponseDto<TUser>> UpdateUsernameAsync(
        string username,
        CancellationToken ct = default)
    {
        var data = new { username };
        string url = $"{_config.API_BASE_URL}{_endpoints.Username}";
        return await _apiService.PostAsync<TUser>(url, data, ct);
    }

    public async UniTask<ApiResponseDto<TUser>> UpdateAvatarAsync(
        string avatarId,
        CancellationToken ct = default)
    {
        var data = new { avatarId };
        string url = $"{_config.API_BASE_URL}{_endpoints.Avatar}";
        return await _apiService.PostAsync<TUser>(url, data, ct);
    }

    public async UniTask<ApiResponseDto<object>> DeactivateUserAsync(
        string reason,
        CancellationToken ct = default)
    {
        var data = new { reason };
        string url = $"{_config.API_BASE_URL}{_endpoints.Deactivate}";
        return await _apiService.PostAsync<object>(url, data, ct);
    }
}
