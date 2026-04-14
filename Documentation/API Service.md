# API Service System Documentation

## Overview
The **API Service** package provides a highly modular, zero-allocation asynchronous networking foundation for Unity using `UniTask` and `UnityWebRequest`. It was engineered with Clean Architecture principles, ensuring that business logic is strictly separated from HTTP transaction logic.

At its core, this package is designed to automatically handle the most painful aspects of game networking: **Silent Token Refreshing** and **Global Error Handling**.

## Core Components

### 1. `GameApiService`
The absolute workhorse of the package. It implements `IGameApiService` and provides `GetAsync`, `PostAsync`, `PutAsync`, and `DeleteAsync`.
- **Automatic Header Injection**: Automatically attaches `Bearer Token`, `x-access-token`, `x-refresh-token`, `x-app-version`, and `x-device-platform` directly from the `IApiConfig` and `IAuthStore`.
- **Recursive Request Interceptor**: Wrapping every REST call is `SendRequestWithInterceptAsync`.

### 2. The Interceptor (`SendRequestWithInterceptAsync`)
This is where the magic happens. The interceptor is a middleware that evaluates UnityWebRequest results *before* sending them back to your Presenter/UI layer:
1. **Access Token Expiration**: If a request returns `401` or "Access token expired", the interceptor pauses the outgoing queue, pings the `/refresh` backend endpoint with the Refresh Token, updates the `IAuthStore` invisibly, and then **re-fires the original request**. From the perspective of the Presenter making the call, the request just took a bit longer, completely hiding the token lifecycle complexity!
2. **Global Errors**: If the server returns catastrophic errors (e.g. `503 Maintenance`, `502 Server Error`, `426 Force Update`), the interceptor blocks the UI flow by invoking methods on the injected `IApiErrorHandler` interface.
3. **Network Drops**: If internet is lost and an `Exception` is thrown, the interceptor triggers an asynchronous UI Prompt (`ShowNetworkErrorPromptAsync`). If the end-user taps "Retry", the interceptor recursively dials the original endpoint again.

### 3. Base DTOs (`ApiResponseDto<T>`)
Every API request returns a standardized `ApiResponseDto<T>`. This wrapper ensures type safety and includes flags like `IsSuccess`, `Message`, `Code`, and `HandledByGlobalUI`.

When `HandledByGlobalUI` is true (for instance, the server was under Maintenance), the UI Presenters are instructed to simply abort their local `try/catch` UI updates, because the `GlobalUIService` has already spawned a blocking popup.

## Integration / Adapters

Because the package is pure, it does not know what your "UI Loading Screen" looks like, nor where your game stores Access Tokens. Therefore, your main Unity project MUST provide implementations for:
- `IAuthStore`: To persist and retrieve Access/Refresh tokens.
- `IApiErrorHandler`: To draw UI popups (Maintenance, Network Error, Update).
- `IApiConfig`: To supply static data (`API_BASE_URL`, `API_VERSION`).

These interfaces are resolved via Dependency Injection (VContainer) when `GameApiService` is constructed.