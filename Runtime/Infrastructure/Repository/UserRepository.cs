using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public class UserRepository<TUser> : IUserRepository<TUser> where TUser : UserData
{
    #region User Data
    // Data disimpan di sini (In-Memory)
    private TUser _cachedUser;

    private readonly IUserApiService<TUser> _userApiService;

    public event Action<TUser> OnUserDataUpdated;

    public TUser CurrentUser => _cachedUser;

    public UserRepository(IUserApiService<TUser> userApiService)
    {
        _userApiService = userApiService;
    }

    public async UniTask<TUser> GetUserData(bool forceRefresh = false, CancellationToken ct = default)
    {
        // Jika data sudah ada di gudang dan tidak minta refresh, berikan yang ada
        if (_cachedUser != null && !forceRefresh)
            return _cachedUser;

        // Jika belum ada, ambil dari API Service
        var result = await _userApiService.GetUserAsync(ct);
        if (!result.IsSuccess)
            throw new Exception(result.ErrorMessage);

        _cachedUser = result.Data;
        Notify();
        return _cachedUser;
    }

    public async UniTask<TUser> UpdateUsername(string username, CancellationToken ct = default)
    {
        var result = await _userApiService.UpdateUsernameAsync(username, ct);
        if (!result.IsSuccess)
            throw new Exception(result.ErrorMessage);

        // Cache kosong (belum pernah GetUserData): tidak ada yang di-patch, return null.
        if (_cachedUser != null)
        {
            _cachedUser.Username = username;
            Notify();
        }
        return _cachedUser;
    }

    public async UniTask<TUser> UpdateAvatar(string avatarId, CancellationToken ct = default)
    {
        var result = await _userApiService.UpdateAvatarAsync(avatarId, ct);
        if (!result.IsSuccess)
            throw new Exception(result.ErrorMessage);

        if (_cachedUser != null)
        {
            _cachedUser.AvatarId = avatarId;
            Notify();
        }
        return _cachedUser;
    }

    public async UniTask<ApiResponseDto<object>> DeactivateUser(string reason, CancellationToken ct = default)
    {
        var result = await _userApiService.DeactivateUserAsync(reason, ct);

        // Jika sukses di server, bersihkan cache lokal
        if (result.IsSuccess)
        {
            ClearData();
            LoggerService.Info("User session cleared from repository cache.");
        }

        return result;
    }

    public void UpdateCachedUser(Action<TUser> updateAction)
    {
        if (_cachedUser == null)
            return;

        updateAction.Invoke(_cachedUser);

        Notify(); // Trigger event untuk update UI

        LoggerService.LogDebug("Cached user data updated and UI refresh triggered.");
    }

    public void ClearData()
    {
        _cachedUser = null;
        Notify();
    }

    // Satu-satunya tempat event dipicu: tiap mutasi memanggilnya tepat sekali.
    private void Notify()
    {
        OnUserDataUpdated?.Invoke(_cachedUser);
    }
    #endregion
}
