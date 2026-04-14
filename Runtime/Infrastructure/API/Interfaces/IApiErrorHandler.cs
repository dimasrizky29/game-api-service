using Cysharp.Threading.Tasks;

namespace Domain.Api.Interfaces
{
    public interface IApiErrorHandler
    {
        UniTask<bool> ShowNetworkErrorPromptAsync();
        void ShowMaintenance();
        void ShowUpdateRequired();
        void ShowServerError();
    }
}
