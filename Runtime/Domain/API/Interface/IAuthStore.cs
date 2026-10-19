using System;

public interface IAuthStore
{
    UserSession Session { get; }

    bool IsLoggedIn { get; }

    /// <summary>Refresh token persisted on this device ("" when none). Survives app restarts.</summary>
    string StoredRefreshToken { get; }

    event Action<UserSession> OnSessionChanged;
    void SetSession(string accessToken, string refreshToken, int expiresInSeconds = 3600);
    void ClearSession();
    bool IsTokenExpired();
}

/// <summary>
/// Ultra-lightweight session data structure. 
/// Using a struct for zero-allocation when passing session info around.
/// </summary>
public struct UserSession
{
    public string AccessToken;
    public string RefreshToken;
    public DateTime Expiry;
    public bool IsAuthenticated => !string.IsNullOrEmpty(AccessToken);
}
