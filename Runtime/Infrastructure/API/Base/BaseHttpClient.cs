using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.Networking;
using VContainer;

namespace Infrastructure.Networking
{
    /// <summary>HTTP status code plus raw body, for success and 4xx/5xx alike.</summary>
    public readonly struct HttpResponse
    {
        public readonly long Code;
        public readonly string Body;

        public HttpResponse(long code, string body)
        {
            Code = code;
            Body = body;
        }
    }

    /// <summary>Thrown when no HTTP response was received (no connection, DNS, timeout).</summary>
    public sealed class NetworkException : Exception
    {
        public NetworkException(string message) : base(message) { }
    }

    public interface IHttpClient
    {
        /// <summary>
        /// Returns the response for any HTTP status (including 4xx/5xx).
        /// Throws <see cref="NetworkException"/> when there is no response at all.
        /// </summary>
        UniTask<HttpResponse> SendAsync(UnityWebRequest request, CancellationToken ct);
    }

    public class BaseHttpClient : IHttpClient
    {
        [Inject]
        public BaseHttpClient() { }

        public async UniTask<HttpResponse> SendAsync(UnityWebRequest request, CancellationToken ct)
        {
            try
            {
                await request.SendWebRequest().WithCancellation(ct);
            }
            catch (UnityWebRequestException)
            {
                // Connection problems (timeout, no internet, DNS) have no usable response.
                if (request.result == UnityWebRequest.Result.ConnectionError ||
                    request.result == UnityWebRequest.Result.DataProcessingError)
                {
                    throw new NetworkException("NETWORK_ERROR");
                }
                // 4xx/5xx: UniTask throws, but the body is still needed by the layer above.
            }

            return new HttpResponse(request.responseCode, request.downloadHandler?.text);
        }
    }
}
