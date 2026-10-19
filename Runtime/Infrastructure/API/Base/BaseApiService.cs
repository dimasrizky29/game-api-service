using System;
using Infrastructure.Networking;

namespace Domain.Api.Base
{
    public abstract class BaseApiService
    {
        public static event Action OnNetworkErrorOccurred;

        protected BaseApiService() { }

        protected void HandleTechnicalError(Exception ex)
        {
            if (IsNetworkError(ex))
            {
                LoggerService.Warning("[BaseApi] Network connection lost.");
                OnNetworkErrorOccurred?.Invoke();
            }
        }

        protected bool IsNetworkError(Exception ex) => ex is NetworkException;
    }
}
