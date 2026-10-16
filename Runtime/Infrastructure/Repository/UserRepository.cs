using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public class UserRepository<TUser> : IUserRepository<TUser> where TUser : UserData
{
    #region User Data 
    // Data disimpan di sini (In-Memory)
    private TUser _cachedUser;
    private TUser ChachedUser
    {
        get => _cachedUser;
        set
        {
            _cachedUser = value;
            OnUserDataUpdated?.Invoke(_cachedUser); // Beritahu yang subscribe kalau data berubah
        }
    }

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
        if (ChachedUser != null && !forceRefresh)
            return ChachedUser;

        // Jika belum ada, ambil dari API Service
        var result = await _userApiService.GetUserAsync(ct);
        if (result.IsSuccess)
        {
            ChachedUser = result.Data;
            return ChachedUser;
        }

        // Jika gagal, kamu bisa melempar Exception atau menggunakan pola Result
        throw new Exception(result.ErrorMessage ?? "Failed to fetch user data");
    }

    public async UniTask<TUser> UpdateUsername(string username, CancellationToken ct = default)
    {
        var result = await _userApiService.UpdateUsernameAsync(username, ct);
        if (result.IsSuccess)
        {
            ChachedUser.Username = username;
            ForceRefreshUI();
            return ChachedUser;
        }

        // Jika gagal, kamu bisa melempar Exception atau menggunakan pola Result
        throw new Exception(result.ErrorMessage ?? "Failed to update username");
    }

    public async UniTask<TUser> UpdateAvatar(string avatarId, CancellationToken ct = default)
    {
        var result = await _userApiService.UpdateAvatarAsync(avatarId, ct);
        if (result.IsSuccess)
        {
            ChachedUser.AvatarId = avatarId;
            ForceRefreshUI();
            return ChachedUser;
        }

        // Jika gagal, kamu bisa melempar Exception atau menggunakan pola Result
        throw new Exception(result.ErrorMessage ?? "Failed to update avatar");
    }

    public async UniTask<ApiResponseDto<object>> DeactivateUser(string reason, CancellationToken ct = default)
    {
        var result = await _userApiService.DeactivateUserAsync(reason, ct);

        // 2. Jika sukses di server, bersihkan cache lokal
        if (result.IsSuccess)
        {
            ChachedUser = null; // Data dihapus dari "Gudang"
            LoggerService.Info("User session cleared from repository cache.");
        }

        return result;
    }

    public void UpdateCachedUser(Action<TUser> updateAction)
    {
        if (_cachedUser == null)
            return;

        updateAction.Invoke(_cachedUser);

        ForceRefreshUI(); // Trigger event untuk update UI

        LoggerService.LogDebug("Cached user data updated and UI refresh triggered.");
    }

    private void ForceRefreshUI()
    {
        OnUserDataUpdated?.Invoke(_cachedUser);
    }

    public void ClearData()
    {
        _cachedUser = null;
    }
    #endregion
}