using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using Domain.Api.Interfaces;
using Infrastructure.Networking;
using NSubstitute;
using NUnit.Framework;
using UnityEngine.Networking;

namespace Game.Tests.EditMode.Infrastructure
{
    public class GameApiServiceTests
    {
        private const string Base = "https://mock-api.com/api";
        private const string DataUrl = Base + "/data";
        private const string RefreshUrl = Base + "/auth/refresh";
        private const string RefreshOkJson = "{\"status\":\"success\",\"data\":{\"accessToken\":\"at2\"}}";

        private IHttpClient _http;
        private IAuthStore _auth;
        private IApiErrorHandler _errors;
        private GameApiService _svc;

        private Func<UnityWebRequest, UniTask<HttpResponse>> _route;
        private List<string> _sent;
        private bool _refreshed;

        [SetUp]
        public void Setup()
        {
            _sent = new List<string>();
            _refreshed = false;

            var config = Substitute.For<IApiConfig>();
            config.API_BASE_URL.Returns(Base);

            _auth = Substitute.For<IAuthStore>();
            _auth.IsLoggedIn.Returns(true);
            _auth.Session.Returns(new UserSession { AccessToken = "at", RefreshToken = "rt" });
            _auth.When(a => a.SetSession(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>()))
                 .Do(_ => _refreshed = true);

            _errors = Substitute.For<IApiErrorHandler>();

            _http = Substitute.For<IHttpClient>();
            _http.SendAsync(Arg.Any<UnityWebRequest>(), Arg.Any<CancellationToken>())
                 .Returns(ci =>
                 {
                     var req = ci.Arg<UnityWebRequest>();
                     _sent.Add(req.url);
                     return _route(req);
                 });

            _svc = new GameApiService(_http, _auth, config, _errors, new ApiEndpoints());
        }

        private static UniTask<HttpResponse> Reply(long code, string body) =>
            UniTask.FromResult(new HttpResponse(code, body));

        // /data answers 401 until a refresh succeeded, then 200. The refresh answer is supplied by the test.
        private void RouteDataBehindRefresh(Func<UniTask<HttpResponse>> refreshAnswer)
        {
            _route = req => req.url == RefreshUrl
                ? refreshAnswer()
                : _refreshed ? Reply(200, "{\"status\":\"success\"}") : Reply(401, "");
        }

        private int Sent(string url) => _sent.FindAll(u => u == url).Count;

        [Test]
        public async Task Get_401ThenRefreshOk_RetriesAndSucceeds()
        {
            RouteDataBehindRefresh(() => Reply(200, RefreshOkJson));

            var result = await _svc.GetAsync<object>(DataUrl);

            Assert.IsTrue(result.IsSuccess);
            Assert.AreEqual(1, Sent(RefreshUrl));
            _auth.Received(1).SetSession("at2", "rt", Arg.Any<int>());
        }

        [Test]
        public async Task TwoConcurrent401s_RefreshOnlyOnce()
        {
            var gate = new UniTaskCompletionSource<HttpResponse>();
            RouteDataBehindRefresh(() => gate.Task);

            var a = _svc.GetAsync<object>(DataUrl);
            var b = _svc.GetAsync<object>(DataUrl);
            gate.TrySetResult(new HttpResponse(200, RefreshOkJson));

            Assert.IsTrue((await a).IsSuccess);
            Assert.IsTrue((await b).IsSuccess);
            Assert.AreEqual(1, Sent(RefreshUrl));
        }

        [Test]
        public async Task RefreshBodyError_ClearsSessionOnce_AllCallersHandled()
        {
            var gate = new UniTaskCompletionSource<HttpResponse>();
            RouteDataBehindRefresh(() => gate.Task);

            var a = _svc.GetAsync<object>(DataUrl);
            var b = _svc.GetAsync<object>(DataUrl);
            gate.TrySetResult(new HttpResponse(200, "{\"status\":\"error\"}"));

            var ra = await a;
            var rb = await b;
            _auth.Received(1).ClearSession();
            Assert.IsTrue(ra.HandledByGlobalUI);
            Assert.IsTrue(rb.HandledByGlobalUI);
            Assert.IsFalse(ra.IsSuccess);
            Assert.IsFalse(rb.IsSuccess);
        }

        [Test]
        public async Task NetworkErrorDuringRefresh_DoesNotClearSession()
        {
            _errors.ShowNetworkErrorPromptAsync().Returns(UniTask.FromResult(false));
            RouteDataBehindRefresh(() => throw new NetworkException("NETWORK_ERROR"));

            var result = await _svc.GetAsync<object>(DataUrl);

            _auth.DidNotReceive().ClearSession();
            Assert.AreEqual(0, result.Code);
            Assert.IsTrue(result.HandledByGlobalUI);
        }

        [Test]
        public async Task CanceledWaiter_DoesNotBreakRefreshForOthers()
        {
            var gate = new UniTaskCompletionSource<HttpResponse>();
            RouteDataBehindRefresh(() => gate.Task);
            using var cts = new CancellationTokenSource();

            var a = _svc.GetAsync<object>(DataUrl);
            var b = _svc.GetAsync<object>(DataUrl, cts.Token);
            cts.Cancel();
            gate.TrySetResult(new HttpResponse(200, RefreshOkJson));

            Assert.IsTrue((await a).IsSuccess);
            Assert.AreEqual("Canceled", (await b).Message);
        }

        [Test]
        public async Task NetworkError_RetriesAtMostMaxTimes()
        {
            _errors.ShowNetworkErrorPromptAsync().Returns(UniTask.FromResult(true));
            _route = _ => throw new NetworkException("NETWORK_ERROR");

            var result = await _svc.GetAsync<object>(DataUrl);

            Assert.AreEqual(1 + GameApiService.MaxNetworkRetries, Sent(DataUrl));
            Assert.AreEqual(0, result.Code);
            Assert.IsTrue(result.HandledByGlobalUI);
        }

        [Test]
        public async Task NetworkError_UserDeclines_NoRetry()
        {
            _errors.ShowNetworkErrorPromptAsync().Returns(UniTask.FromResult(false));
            _route = _ => throw new NetworkException("NETWORK_ERROR");

            await _svc.GetAsync<object>(DataUrl);

            Assert.AreEqual(1, Sent(DataUrl));
        }

        [Test]
        public async Task Html502Body_ShowsServerError()
        {
            _route = _ => Reply(502, "<html>Bad Gateway</html>");

            var result = await _svc.GetAsync<object>(DataUrl);

            _errors.Received(1).ShowServerError();
            Assert.AreEqual(502, result.Code);
            Assert.IsTrue(result.HandledByGlobalUI);
        }

        [Test]
        public async Task Http200HtmlBody_IsNotSuccess()
        {
            _route = _ => Reply(200, "<html>Sign in to Wi-Fi</html>");

            var result = await _svc.GetAsync<TestUser>(DataUrl);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Invalid server response", result.ErrorMessage);
            Assert.AreEqual(200, result.Code);
        }

        [Test]
        public async Task Http200EnvelopeWithMismatchedData_KeepsBackendError()
        {
            _route = _ => Reply(200, "{\"status\":\"error\",\"message\":\"Insufficient balance\",\"data\":[]}");

            var result = await _svc.GetAsync<TestUser>(DataUrl);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Insufficient balance", result.ErrorMessage);
        }

        [Test]
        public async Task Http200SuccessEnvelopeWithMismatchedData_IsNotSuccess()
        {
            _route = _ => Reply(200, "{\"status\":\"success\",\"data\":[]}");

            var result = await _svc.GetAsync<TestUser>(DataUrl);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Invalid server response", result.ErrorMessage);
        }

        [Test]
        public async Task Refresh503_DoesNotClearSession_ShowsMaintenance()
        {
            RouteDataBehindRefresh(() => Reply(503, ""));

            var result = await _svc.GetAsync<object>(DataUrl);

            _auth.DidNotReceive().ClearSession();
            _errors.Received(1).ShowMaintenance();
            Assert.IsTrue(result.HandledByGlobalUI);
            Assert.IsFalse(result.IsSuccess);
        }

        [Test]
        public async Task RefreshFinishingAfterLogout_DoesNotRestoreSession()
        {
            var gate = new UniTaskCompletionSource<HttpResponse>();
            RouteDataBehindRefresh(() => gate.Task);

            var call = _svc.GetAsync<object>(DataUrl);
            _auth.IsLoggedIn.Returns(false); // user logged out while the refresh was in flight
            gate.TrySetResult(new HttpResponse(200, RefreshOkJson));

            var result = await call;
            _auth.DidNotReceive().SetSession(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<int>());
            _auth.DidNotReceive().ClearSession();
            Assert.IsTrue(result.HandledByGlobalUI);
        }

        [Test]
        public async Task ConcurrentNetworkFailures_PromptOnce_AllCallersResume()
        {
            var promptGate = new UniTaskCompletionSource<bool>();
            _errors.ShowNetworkErrorPromptAsync().Returns(promptGate.Task);
            bool online = false;
            _route = _ => online
                ? Reply(200, "{\"status\":\"success\"}")
                : throw new NetworkException("NETWORK_ERROR");

            var a = _svc.GetAsync<object>(DataUrl);
            var b = _svc.GetAsync<object>(DataUrl);
            online = true;
            promptGate.TrySetResult(true);

            Assert.IsTrue((await a).IsSuccess);
            Assert.IsTrue((await b).IsSuccess);
            await _errors.Received(1).ShowNetworkErrorPromptAsync();
        }

        [Test]
        public async Task Http200BodyError_IsNotSuccess_NoGlobalPopup()
        {
            _route = _ => Reply(200, "{\"status\":\"error\",\"message\":\"Insufficient balance\"}");

            var result = await _svc.GetAsync<object>(DataUrl);

            Assert.IsFalse(result.IsSuccess);
            Assert.AreEqual("Insufficient balance", result.ErrorMessage);
            Assert.IsFalse(result.HandledByGlobalUI);
        }
    }
}
