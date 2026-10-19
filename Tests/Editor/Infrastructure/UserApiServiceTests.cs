using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Game.Tests.EditMode.Infrastructure
{
    public class UserApiServiceTests
    {
        private const string BaseUrl = "https://mock-api.com/api";

        private UserApiService<TestUser> _service;
        private IGameApiService _mockGameApiService;

        [SetUp]
        public void Setup()
        {
            _mockGameApiService = Substitute.For<IGameApiService>();
            var config = Substitute.For<IApiConfig>();
            config.API_BASE_URL.Returns(BaseUrl);

            _service = new UserApiService<TestUser>(_mockGameApiService, config, new ApiEndpoints());
        }

        [Test]
        public async Task GetUserAsync_Calls_GetAsync_OnUsersUrl()
        {
            _mockGameApiService
                .GetAsync<TestUser>($"{BaseUrl}/users", default)
                .Returns(UniTask.FromResult(new ApiResponseDto<TestUser>()));

            await _service.GetUserAsync(default);

            await _mockGameApiService.Received(1).GetAsync<TestUser>($"{BaseUrl}/users", default);
        }

        [Test]
        public async Task UpdateUsernameAsync_Posts_ToUsernameUrl()
        {
            _mockGameApiService
                .PostAsync<TestUser>($"{BaseUrl}/users/username", Arg.Any<object>(), default)
                .Returns(UniTask.FromResult(new ApiResponseDto<TestUser>()));

            await _service.UpdateUsernameAsync("bob", default);

            await _mockGameApiService.Received(1).PostAsync<TestUser>($"{BaseUrl}/users/username", Arg.Any<object>(), default);
        }

        [Test]
        public async Task UpdateAvatarAsync_Posts_ToAvatarUrl()
        {
            _mockGameApiService
                .PostAsync<TestUser>($"{BaseUrl}/users/avatar", Arg.Any<object>(), default)
                .Returns(UniTask.FromResult(new ApiResponseDto<TestUser>()));

            await _service.UpdateAvatarAsync("a1", default);

            await _mockGameApiService.Received(1).PostAsync<TestUser>($"{BaseUrl}/users/avatar", Arg.Any<object>(), default);
        }
    }
}
