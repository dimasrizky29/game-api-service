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

        private async Task<TestUser> Load()
        {
            var user = new TestUser { Username = "old", AvatarId = "a0" };
            _api.GetUserAsync(default).Returns(UniTask.FromResult(Ok(user)));
            await _repo.GetUserData();
            _events = 0;
            return user;
        }

        [Test]
        public async Task UpdateUsername_AfterLoad_PatchesCacheAndFiresOnce()
        {
            await Load();
            _api.UpdateUsernameAsync("new", default).Returns(UniTask.FromResult(Ok(null)));

            await _repo.UpdateUsername("new");

            Assert.AreEqual("new", _repo.CurrentUser.Username);
            Assert.AreEqual(1, _events);
        }

        [Test]
        public async Task UpdateAvatar_AfterLoad_PatchesCacheAndFiresOnce()
        {
            await Load();
            _api.UpdateAvatarAsync("a1", default).Returns(UniTask.FromResult(Ok(null)));

            await _repo.UpdateAvatar("a1");

            Assert.AreEqual("a1", _repo.CurrentUser.AvatarId);
            Assert.AreEqual(1, _events);
        }

        [Test]
        public async Task UpdateUsername_BeforeLoad_DoesNotThrow_ReturnsNull_NoEvent()
        {
            _api.UpdateUsernameAsync("new", default).Returns(UniTask.FromResult(Ok(null)));

            var result = await _repo.UpdateUsername("new");

            Assert.IsNull(result);
            Assert.AreEqual(0, _events);
            await _api.Received(1).UpdateUsernameAsync("new", default);
        }

        [Test]
        public async Task UpdateUsername_Failure_ThrowsResponseMessage_CacheUnchanged()
        {
            await Load();
            _api.UpdateUsernameAsync("new", default)
                .Returns(UniTask.FromResult(ApiResponseDto<TestUser>.CreateError("nope", 400, false)));

            var ex = Assert.ThrowsAsync<Exception>(async () => await _repo.UpdateUsername("new"));

            Assert.AreEqual("nope", ex.Message);
            Assert.AreEqual("old", _repo.CurrentUser.Username);
            Assert.AreEqual(0, _events);
        }

        [Test]
        public async Task ClearData_NullsCache_AndFiresEventWithNull()
        {
            await Load();
            TestUser received = new TestUser();
            _repo.OnUserDataUpdated += u => received = u;

            _repo.ClearData();

            Assert.IsNull(_repo.CurrentUser);
            Assert.IsNull(received);
            Assert.AreEqual(1, _events);
        }

        [Test]
        public async Task DeactivateUser_Success_ClearsCache_FiresOnceWithNull()
        {
            await Load();
            _api.DeactivateUserAsync("bye", default)
                .Returns(UniTask.FromResult(new ApiResponseDto<object> { Status = "success" }));

            await _repo.DeactivateUser("bye");

            Assert.IsNull(_repo.CurrentUser);
            Assert.AreEqual(1, _events);
        }

        [Test]
        public void UpdateCachedUser_WhenEmpty_NoOpAndNoEvent()
        {
            var called = false;

            _repo.UpdateCachedUser(_ => called = true);

            Assert.IsFalse(called);
            Assert.AreEqual(0, _events);
        }

        [Test]
        public async Task UpdateCachedUser_AfterLoad_AppliesActionAndFiresOnce()
        {
            await Load();

            _repo.UpdateCachedUser(u => u.Username = "patched");

            Assert.AreEqual("patched", _repo.CurrentUser.Username);
            Assert.AreEqual(1, _events);
        }
    }
}
