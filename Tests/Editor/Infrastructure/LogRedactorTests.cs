using NUnit.Framework;

namespace Game.Tests.EditMode.Infrastructure
{
    public class LogRedactorTests
    {
        [Test]
        public void Redacts_JsonSecrets() =>
            Assert.AreEqual(
                "{\"email\":\"a@b.c\",\"passwordSalt\":\"***\",\"data\":{\"AccessToken\":\"***\"}}",
                LogRedactor.Redact("{\"email\":\"a@b.c\",\"passwordSalt\":\"pw123salt\",\"data\":{\"AccessToken\":\"eyJ\"}}"));

        [Test]
        public void Redacts_QueryTokens() =>
            Assert.AreEqual(
                "wss://x/ws?uid=1&accessToken=***&refreshToken=***",
                LogRedactor.Redact("wss://x/ws?uid=1&accessToken=aaa&refreshToken=bbb"));

        [Test]
        public void LeavesOtherTextUntouched() =>
            Assert.AreEqual("{\"username\":\"bob\"}", LogRedactor.Redact("{\"username\":\"bob\"}"));

        [Test]
        public void Null_ReturnsNull() => Assert.IsNull(LogRedactor.Redact(null));
    }
}
