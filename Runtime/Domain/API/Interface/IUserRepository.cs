using System;
using System.Threading;
using Cysharp.Threading.Tasks;

public interface IUserRepository<TUser> where TUser : UserData
{
    // --- User Data ---
    TUser CurrentUser { get; }

    UniTask<ApiResponseDto<object>> DeactivateUser(string reason, CancellationToken ct = default);
    UniTask<TUser> GetUserData(bool forceRefresh = false, CancellationToken ct = default);
    UniTask<TUser> UpdateAvatar(string avatarId, CancellationToken ct = default);
    UniTask<TUser> UpdateUsername(string username, CancellationToken ct = default);
    void UpdateCachedUser(Action<TUser> updateAction);

    event Action<TUser> OnUserDataUpdated;

    void ClearData();
}
