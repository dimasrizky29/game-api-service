# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

## [0.2.2] - 2026-10-20

### Fixed
- Five `.meta` files were missing from the `v0.2.1` git tag (`docs.meta` and the four test files in `Tests/Editor/Infrastructure`). Unity logs `has no meta file, but it's in an immutable folder` for them when the package is installed from git, which also makes batch test runs exit with an error. Use `v0.2.2` or later instead of `v0.2.1`.

## [0.2.1] - 2026-10-19

### Breaking
- `IHttpClient.SendAsync` returns `HttpResponse` (code + body) instead of `string`; connection failures throw `NetworkException`.
- `AuthApiService`, `UserApiService<TUser>` and `GameApiService` constructors take `ApiEndpoints`. Register it: `builder.RegisterInstance(new ApiEndpoints());`.
- `IAuthStore` gains `StoredRefreshToken`.
- `DeleteAsync` returns true only when the call succeeded (was true for any non-null result).

### Fixed
- `ApiResponseDto.IsSuccess` honours the body `status` first; a body with no `status` is success only for 2xx. HTTP 200 with `status: "error"` is no longer success.
- Token refresh is single-flight and always uses `ApiEndpoints.Refresh` (default `/auth/refresh`; the old `/refresh` is gone). Concurrent callers get the real outcome, and a failed refresh clears the session exactly once.
- A network failure during token refresh no longer logs the user out; it goes through the normal network-retry prompt.
- Network retry is capped at `GameApiService.MaxNetworkRetries` (3) resends instead of recursing without limit.
- Non-JSON error bodies (e.g. a 502 HTML page) keep their HTTP code, so the maintenance/force-update/server-error popups still show.
- A non-empty body that is not a usable envelope (captive-portal HTML on a 2xx, or `data` of the wrong shape) is an error (`"Invalid server response"`), never a success with null data. A readable `status`/`message` from the backend is kept.
- Token refresh only ends the session when the refresh token is rejected (400/401/403/440 or an explicit error status). 5xx, 426 and unreadable replies keep the session and show the matching global popup instead of logging the user out.
- A refresh that completes after logout or an account switch no longer restores the old session.
- Concurrent callers that hit a network error share one retry prompt; all of them resume after the answer.
- Tokens, passwords and OAuth tokens are redacted from API logs (`LogRedactor`).
- `AuthStore` clears and persists state before raising `OnSessionChanged`, so listeners see the new state.

### Added
- `ApiEndpoints`: every endpoint path as an overridable default.
- `AuthStore.RefreshTokenKey` and `IAuthStore.StoredRefreshToken`: one owner for the persisted refresh token.

### Security note
- Projects that ever committed `.env.dev` should rotate `API_BEARER` and `API_SALT_PASSWORD`.

### Migration
- Games that reference this package by git URL without a tag follow `main`: pin them to `#v0.1.2` until they are ready to adopt 0.2.x.

## [0.2.0] - 2026-10-16

### Breaking
- `UserData` now only holds `uid`, `gameId`, `username`, `refCode`, `avatarId`. `MailData`, `BalanceShop`, `BalanceGame` and the nested/top-level `Balance`, `BalanceEarn`, `Mail` classes are removed: define them in your own `UserData` subclass.
- `IUserApiService`, `UserApiService`, `IUserRepository`, `UserRepository` are now generic (`<TUser> where TUser : UserData`). Register the closed types, e.g. `UserRepository<MyUserData>`.

### Fixed
- `UserRepository` no longer throws `NullReferenceException` when updating username/avatar before the user is loaded.
- `OnUserDataUpdated` fires exactly once per mutation; `ClearData()` now fires it too (with `null`).
- Test asmdefs referenced a non-existent assembly.

### Changed
- Sample replaced with a project-specific `UserData` example. README and package metadata updated; `com.unity.nuget.newtonsoft-json` declared as a dependency.

## [0.1.0] - 2026-04-13

### This is the first release of *\<API Service\>*.

*Short description of this release*
