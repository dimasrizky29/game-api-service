using System.Collections;
using System.Threading.Tasks;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Game.Tests.PlayMode
{
    public class SamplePlayModeTest
    {
        private IGameApiService _mockGameApiService;
        private IApiConfig _mockApiConfig;
        private AuthApiService _authService;

        [SetUp]
        public void Setup()
        {
            // Just like EditMode, we set up our mocks first
            _mockGameApiService = Substitute.For<IGameApiService>();
            _mockApiConfig = Substitute.For<IApiConfig>();

            _mockApiConfig.API_BASE_URL.Returns("https://fake-api.com");
            _mockApiConfig.API_SALT_PASSWORD.Returns("salt");

            _authService = new AuthApiService(_mockGameApiService, _mockApiConfig);
        }

        // [UnityTest] is exactly what you need to test GameObjects or components over time.
        // It returns an IEnumerator. Because AuthApiService uses UniTask, we can convert 
        // UniTask to an IEnumerator using .ToCoroutine()
        [UnityTest]
        public IEnumerator Login_SimulatedInPlayMode_Succeeds()
        {
            // 1. Arrange Mock Data
            var fakeResponse = new ApiResponseDto<SignInDataDto> { Message = "PlayMode Login Success" };
            
            _mockGameApiService
                .PostAsync<SignInDataDto>(Arg.Any<string>(), Arg.Any<object>(), default)
                .Returns(UniTask.FromResult(fakeResponse));

            // 2. Act
            // Since this is a UnityTest returning IEnumerator, we capture the result inside an async helper, 
            // or simply wait for the UniTask using .ToCoroutine()
            bool isFinished = false;
            ApiResponseDto<SignInDataDto> result = null;

            // Start the async operation without blocking the main Unity thread
            UniTask.Void(async () => 
            {
                result = await _authService.LoginAsync("test@email.com", "password123");
                isFinished = true;
            });

            // PlayMode magic: Wait until the async task sets our boolean!
            // This allows the Unity Engine's Update loops, Physics, and Render loops to keep running.
            yield return new WaitUntil(() => isFinished);

            // 3. Assert
            Assert.IsNotNull(result);
            Assert.AreEqual("PlayMode Login Success", result.Message);
        }

        // You also can still use exact normal asynchronous Tests inside the PlayMode assembly:
        [Test]
        public async Task Login_AsyncTask_InPlayModeEnvironment()
        {
            // Even though this is inside the PlayMode folder, using [Test] async Task works exactly 
            // like it did in EditMode - but it runs within the booted-up player!
            var fakeResponse = new ApiResponseDto<SignInDataDto> { Message = "Normal Async Success" };
            
            _mockGameApiService
                .PostAsync<SignInDataDto>(Arg.Any<string>(), Arg.Any<object>(), default)
                .Returns(UniTask.FromResult(fakeResponse));

            var result = await _authService.LoginAsync("test@email.com", "pass");
            Assert.AreEqual("Normal Async Success", result.Message);
        }
    }
}

