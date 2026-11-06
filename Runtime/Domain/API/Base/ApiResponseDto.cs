using Newtonsoft.Json;

public class ApiResponseDto<T>
{
    [JsonProperty("success")]
    public bool Success { get; set; }

    [JsonProperty("message")]
    public string Message { get; set; }

    [JsonProperty("data")]
    public T Data { get; set; }

    [JsonProperty("meta")]
    public MetaData Meta { get; set; } //for pagination, optional

    // --- Metadata Internal (Bukan dari JSON) ---
    [JsonIgnore]
    public int Code { get; set; }

    [JsonIgnore] 
    public bool HandledByGlobalUI { get; private set; }

    #region Helpers

    [JsonIgnore]
    public bool IsSuccess => Success || (Code >= 200 && Code < 300);

    [JsonIgnore]
    public string ErrorMessage => !string.IsNullOrEmpty(Message) ? Message : "An unknown error has occurred.";

    [JsonIgnore]
    public bool IsGlobalError => Code == 426 || // Force Update
                                 Code == 503 || // Maintenance
                                 Code == 502 || // Bad Gateway
                                 Code == 504 || // Gateway Timeout
                                 Code == 0;     // network Physical Error
    #endregion

    // Method Factory untuk mempermudah pembuatan Error DTO secara internal
    public static ApiResponseDto<T> CreateError(string msg, int code, bool handled) => new()
    {
        Success = false,
        Message = msg,
        Code = code,
        HandledByGlobalUI = handled
    };

    public void SetHandledByGlobalUI(bool handled)
    {
        HandledByGlobalUI = handled;
    }
}

#region --- DTO Tambahan untuk API ---

public class MetaData
{
    public int Page { get; set; }
    public int TotalPages { get; set; }
    public int Total { get; set; }
    public int Limit { get; set; }
}

#endregion
