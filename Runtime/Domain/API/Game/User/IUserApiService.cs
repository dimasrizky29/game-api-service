using System.Threading;
using Cysharp.Threading.Tasks;

public interface IUserApiService<TUser> where TUser : UserData
{
    UniTask<ApiResponseDto<TUser>> GetUserAsync(CancellationToken ct = default);
    UniTask<ApiResponseDto<TUser>> UpdateUsernameAsync(string username, CancellationToken ct = default);
    UniTask<ApiResponseDto<TUser>> UpdateAvatarAsync(string avatarId, CancellationToken ct = default);
    UniTask<ApiResponseDto<object>> DeactivateUserAsync(string reason, CancellationToken ct = default);
}
