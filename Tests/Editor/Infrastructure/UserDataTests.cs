using Newtonsoft.Json;
using NUnit.Framework;

namespace Game.Tests.EditMode.Infrastructure
{
    public class UserDataTests
    {
        private const string Json =
            "{\"uid\":\"6f9619ff-8b86-d011-b42d-00cf4fc964ff\",\"username\":\"bob\",\"mail\":{\"unreadCount\":3}}";

        [Test]
        public void Deserialize_ProjectSubclass_ReadsExtraFields()
        {
            var user = JsonConvert.DeserializeObject<TestUser>(Json);

            Assert.AreEqual("bob", user.Username);
            Assert.AreEqual(3, user.Mail.UnreadCount);
        }

        [Test]
        public void Deserialize_BaseType_IgnoresUnknownFields()
        {
            UserData user = null;

            Assert.DoesNotThrow(() => user = JsonConvert.DeserializeObject<UserData>(Json));
            Assert.AreEqual("bob", user.Username);
        }

        [Test]
        public void Deserialize_MissingFields_LeavesDefaults()
        {
            var user = JsonConvert.DeserializeObject<TestUser>("{}");

            Assert.IsNull(user.Uid);
            Assert.IsNull(user.Username);
        }
    }
}
