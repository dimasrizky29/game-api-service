using System;
using UnityEngine;

/// <summary>
/// Global high-performance Auth Store.
/// Acts as the single source of truth for user sessions.
/// </summary>
public class AuthStore : IAuthStore
{
    /// <summary>PlayerPrefs key of the persisted refresh token (kept stable so existing players stay logged in).</summary>
    public const string RefreshTokenKey = "auth-token";

    // High-performance event for UI/Router updates
    public event Action<UserSession> OnSessionChanged;

    private UserSession _currentSession;
    public UserSession Session => _currentSession;
    public bool IsLoggedIn => _currentSession.IsAuthenticated;
    public string StoredRefreshToken => PlayerPrefs.GetString(RefreshTokenKey, "");

    /// <summary>
    /// Sets the session and notifies listeners. O(1) performance.
    /// The refresh token is persisted BEFORE listeners run, so they observe the new state.
    /// </summary>
    public void SetSession(string accessToken, string refreshToken, int expiresInSeconds = 3600)
    {
        //save to PlayerPrefs or secure storage
        if (refreshToken != StoredRefreshToken)
        {
            PlayerPrefs.SetString(RefreshTokenKey, refreshToken);
            PlayerPrefs.Save();
        }

        _currentSession = new UserSession
        {
            AccessToken = accessToken,
            RefreshToken = refreshToken,
            Expiry = DateTime.UtcNow.AddSeconds(expiresInSeconds)
        };

        OnSessionChanged?.Invoke(_currentSession);
    }

    /// <summary>
    /// Clears the session and triggers a logout event.
    /// Persisted state is cleared BEFORE listeners run.
    /// </summary>
    public void ClearSession()
    {
        _currentSession = default; // Reset to empty struct

        //save to PlayerPrefs or secure storage
        PlayerPrefs.DeleteKey(RefreshTokenKey);
        PlayerPrefs.Save();

        OnSessionChanged?.Invoke(_currentSession);
    }

    /// <summary>
    /// Lightweight check to see if the token needs refreshing.
    /// </summary>
    public bool IsTokenExpired()
    {
        if (!IsLoggedIn) return true;
        return DateTime.UtcNow >= _currentSession.Expiry;
    }
}
