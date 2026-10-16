using System;
using Newtonsoft.Json;

/// <summary>
/// Fields every project shares. Subclass this in your project to add your own
/// (mail, balances, etc.) and register the generic services with your subclass.
/// </summary>
public class UserData
{
    [JsonProperty("uid")]
    public Guid? Uid { get; set; }

    [JsonProperty("gameId")]
    public string GameId { get; set; }

    [JsonProperty("username")]
    public string Username { get; set; }

    [JsonProperty("refCode")]
    public string RefCode { get; set; }

    [JsonProperty("avatarId")]
    public string AvatarId { get; set; }
}
