# Changelog
All notable changes to this package will be documented in this file.

The format is based on [Keep a Changelog](http://keepachangelog.com/en/1.0.0/)
and this project adheres to [Semantic Versioning](http://semver.org/spec/v2.0.0.html).

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
