using Newtonsoft.Json;
using VContainer;

// Project-specific user data: add whatever fields your backend returns.
// The package only knows uid, gameId, username, refCode and avatarId.
public class MyUserData : UserData
{
    [JsonProperty("mail")]
    public MailInfo Mail { get; set; }

    [JsonProperty("balance")]
    public BalanceInfo BalanceShop { get; set; }

    public class MailInfo
    {
        [JsonProperty("haveUnreadInbox")]
        public bool HaveUnreadInbox { get; set; }

        [JsonProperty("unreadCount")]
        public int UnreadCount { get; set; }
    }

    public class BalanceInfo
    {
        [JsonProperty("currentBalance")]
        public double CurrentBalance { get; set; }
    }
}

public static class ExampleUserRegistration
{
    // Call from your LifetimeScope.Configure(builder), after IGameApiService and IApiConfig are registered.
    public static void Register(IContainerBuilder builder)
    {
        builder.Register<IUserApiService<MyUserData>, UserApiService<MyUserData>>(Lifetime.Singleton);
        builder.Register<IUserRepository<MyUserData>, UserRepository<MyUserData>>(Lifetime.Singleton);
    }
}
