# Unity Advanced API Service Package

A robust, high-performance, and modular REST API package for Unity projects. Built on top of `UnityWebRequest` and completely integrated with `UniTask` to provide low-allocation asynchronous routines.

## 🚀 Features

- **Asynchronous by Design**: Returns `UniTask<ApiResponseDto<T>>` for memory-efficient `async/await` flows without Coroutine overhead.
- **Silent Token Refreshing**: Built-in interceptor. If a request meets a `401 Unauthorized` token expiration, the package automatically pauses, hits the `/refresh` endpoint, saves the new tokens, and re-executes your original request seamlessly!
- **Global Error Handling**: Automatically traps server-level disasters (`503 Maintenance`, `502 Server Error`, `426 Force Update Required`) and routes them to a `IApiErrorHandler` abstraction so your UI logic doesn't crash or bleed into your networking logic.
- **Network Retry Loop**: If connection drops, it awaits an interactive Prompt from the Global UI, allowing the user to click "Retry" which will recursively replay the failed HTTP request.
- **Clean Architecture Ready**: Designed as pure Domain/Infrastructure. The package has *no hardcoded dependencies* to your game's unique Canvas/UI scripts.

## 🏗 Requirements
- Unity 2021.3 or higher
- [UniTask](https://github.com/Cysharp/UniTask)
- [VContainer](https://vcontainer.hadashikick.jp/) (For Dependency Injection)
- Newtonsoft.Json

## 📦 Installation

Package Manager → Add package from git URL:

```
https://github.com/yatech-studio/game-api-service.git#0.2.0
```

`ServiceInstaller` (Editor) auto-installs UniTask, VContainer and Logger Service if missing.

## 🛠 Setup & Initialization

Because this is a pure package, it requires **Adapter interfaces** to be fulfilled by your main project. Using VContainer, bind your project-specific scripts to the expected interface parameters:

```csharp
// Inside your GameLifetimeScope.cs
using Domain.Api.Interfaces;

public override void Configure(IContainerBuilder builder)
{
    // 1. Register API Configuration
    builder.RegisterInstance<IApiConfig>(myScriptableObjectConfig);

    // 2. Register your Auth Token Manager
    builder.Register<IAuthStore, MyLocalAuthStore>(Lifetime.Singleton);

    // 3. Register your UI Error handler (The Bridge)
    builder.Register<IApiErrorHandler, GlobalUIPresenter>(Lifetime.Singleton);

    // 4. Finally, Register the Package's core services
    builder.Register<IHttpClient, BaseHttpClient>(Lifetime.Singleton);
    builder.Register<IGameApiService, GameApiService>(Lifetime.Singleton);
    builder.Register<IAuthApiService, AuthApiService>(Lifetime.Singleton);

    // 5. User services, closed over YOUR UserData subclass (see below)
    builder.Register<IUserApiService<MyUserData>, UserApiService<MyUserData>>(Lifetime.Singleton);
    builder.Register<IUserRepository<MyUserData>, UserRepository<MyUserData>>(Lifetime.Singleton);
}
```

## 👤 Project-specific user data

The package only knows `uid`, `gameId`, `username`, `refCode`, `avatarId`. Add your own fields by subclassing, then inject `IUserRepository<MyUserData>`:

```csharp
public class MyUserData : UserData
{
    [JsonProperty("mail")] public MailInfo Mail { get; set; }
}
```

Fields the server sends that your class does not declare are ignored; missing fields stay `null`/default. See `Samples~/Example/ExampleUserData.cs`.

## 📚 Usage Example

When `IGameApiService` is successfully injected into your Presenters, making requests is extremely clean:

```csharp
public async UniTaskVoid FetchLeaderboard()
{
    // Make the request. The interceptor handles Tokens & Headers automatically!
    var response = await _apiService.GetAsync<LeaderboardDto>("/v1/leaderboard");

    // Check if the Global UI took over (e.g., Server Maintenance happened)
    if (response.HandledByGlobalUI) return; 

    // Handle normal business response
    if (response.IsSuccess)
    {
        RenderUI(response.Data);
    }
    else
    {
        ShowLocalAlert(response.Message);
    }
}
```

## 🧪 Testing
`NSubstitute`-based EditMode tests cover `ApiConfig`, `AuthApiService`, `UserApiService`, `UserRepository` and `UserData` deserialization. Run via the Unity Test Runner. `BaseHttpClient` (token refresh, global error handling) is not covered yet.