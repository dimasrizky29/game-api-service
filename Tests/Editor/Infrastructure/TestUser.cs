using Newtonsoft.Json;

namespace Game.Tests.EditMode.Infrastructure
{
    public class TestMail
    {
        [JsonProperty("unreadCount")]
        public int UnreadCount { get; set; }
    }

    public class TestUser : UserData
    {
        [JsonProperty("mail")]
        public TestMail Mail { get; set; }
    }
}
