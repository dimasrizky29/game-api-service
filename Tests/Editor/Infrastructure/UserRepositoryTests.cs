using System;
using Cysharp.Threading.Tasks;
using NSubstitute;
using NUnit.Framework;
using System.Threading.Tasks;

namespace Game.Tests.EditMode.Infrastructure
{
    public class UserRepositoryTests
    {
        private IUserApiService<TestUser> _api;
        private UserRepository<TestUser> _repo;
        private int _events;

        private static ApiResponseDto<TestUser> Ok(TestUser user) =>
            new ApiResponseDto<TestUser> { Status = "success", Data = user };

        [SetUp]
        public void Setup()
        {
            _api = Substitute.For<IUserApiService<TestUser>>();
            _repo = new UserRepository<TestUser>(_api);
            _events = 0;
            _repo.OnUserDataUpdated += _ => _events++;
        }

        [Test]
        public async Task GetUserData_FirstCall_FetchesCachesAndFiresEventOnce()
        {
            var user = new TestUser { Username = "bob" };
            _api.GetUserAsync(default).Returns(UniTask.FromResult(Ok(user)));

            var result = await _repo.GetUserData();

            Assert.AreSame(user, result);
            Assert.AreSame(user, _repo.CurrentUser);
            Assert.AreEqual(1, _events);
        }

        [Test]
        public async Task GetUserData_SecondCall_UsesCache()
        {
            _api.GetUserAsync(default).Returns(UniTask.FromResult(Ok(new TestUser())));

            await _repo.GetUserData();
            await _repo.GetUserData();

            await _api.Received(1).GetUserAsync(default);
        }

        [Test]
        public async Task GetUserData_ForceRefresh_Refetches()
        {
            _api.GetUserAsync(default).Returns(UniTask.FromResult(Ok(new TestUser())));

            await _repo.GetUserData();
            await _repo.GetUserData(true);

            await _api.Received(2).GetUserAsync(default);
        }

        [Test]
        public void GetUserData_Failure_ThrowsResponseMessage_AndCacheStaysNull()
        {
            _api.GetUserAsync(default)
                .Returns(UniTask.FromResult(ApiResponseDto<TestUser>.CreateError("boom", 500, false)));

            var ex = Assert.ThrowsAsync<Exception>(async () => await _repo.GetUserData());

            Assert.AreEqual("boom", ex.Message);
            Assert.IsNull(_repo.CurrentUser);
        }

        [Test]
        public void GetUserData_FailureWithEmptyMessage_ThrowsFallbackMessage()
        {
            _api.GetUserAsync(default)
                .Returns(UniTask.FromResult(ApiResponseDto<TestUser>.CreateError("", 500, false)));

            var ex = Assert.ThrowsAsync<Exception>(async () => await _repo.GetUserData());

            Assert.AreEqual("An unknown error has occurred.", ex.Message);
        }
    }
}
