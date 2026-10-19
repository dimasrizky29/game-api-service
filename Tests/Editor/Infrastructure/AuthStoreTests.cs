using NUnit.Framework;
using UnityEngine;

namespace Game.Tests.EditMode.Infrastructure
{
    public class AuthStoreTests
    {
        [SetUp]
        public void SetUp() => PlayerPrefs.DeleteKey(AuthStore.RefreshTokenKey);

        [TearDown]
        public void TearDown() => PlayerPrefs.DeleteKey(AuthStore.RefreshTokenKey);

        [Test]
        public void SetSession_PersistsRefreshToken()
        {
            new AuthStore().SetSession("at", "rt");

            Assert.AreEqual("rt", new AuthStore().StoredRefreshToken);
        }

        [Test]
        public void ClearSession_ListenersSeeClearedState()
        {
            var store = new AuthStore();
            store.SetSession("at", "rt");
            string seenToken = null;
            bool? seenLoggedIn = null;
            store.OnSessionChanged += _ =>
            {
                seenToken = store.StoredRefreshToken;
                seenLoggedIn = store.IsLoggedIn;
            };

            store.ClearSession();

            Assert.AreEqual("", seenToken);
            Assert.AreEqual(false, seenLoggedIn);
        }

        [Test]
        public void SetSession_ListenersSeeNewToken()
        {
            var store = new AuthStore();
            string seenToken = null;
            store.OnSessionChanged += _ => seenToken = store.StoredRefreshToken;

            store.SetSession("at", "rt2");

            Assert.AreEqual("rt2", seenToken);
        }
    }
}
