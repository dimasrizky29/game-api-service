# Modular UserData + Package Upgrade Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Let each game project define its own user fields without editing this package, and fix the `UserRepository` defects and stale docs found in review.

**Architecture:** `UserData` shrinks to the fields every project shares (`uid`, `gameId`, `username`, `refCode`, `avatarId`). `UserApiService`, `IUserApiService`, `UserRepository` and `IUserRepository` become generic over `TUser : UserData`; a project subclasses `UserData` with its own fields and registers the closed generic types in VContainer. `UserRepository` is then made null-safe and raises its event exactly once per mutation. Released as breaking `0.2.0`.

**Tech Stack:** Unity 6000.3, C#, UniTask, VContainer, Newtonsoft.Json, NUnit + NSubstitute (EditMode).

**Spec:** none. The design was agreed in chat (review + recommendation of 2026-10-16) and is summarized in Architecture above.

## Global Constraints

- Package id `com.yatechstudio.apiservice`, Unity `6000.3`; current `package.json` version is `0.1.3` (uncommitted), target `0.2.0`.
- Runtime assembly is `Yatechstudio.Apiservice` (`Runtime/Yatechstudio.Apiservice.asmdef`).
- All package types stay in the global namespace (no namespace migration in this plan).
- Error model unchanged: repository methods throw `Exception(result.ErrorMessage)` on failed responses.
- No new dependencies except `com.unity.nuget.newtonsoft-json` declared in `package.json` (already used in code).
- Test namespace `Game.Tests.EditMode.Infrastructure`, files under `Tests/Editor/Infrastructure/`, mocks via NSubstitute, same style as `AuthApiServiceTests.cs` (mock base URL `https://mock-api.com/api`).
- Tests run from a host Unity project that embeds this package; there is no Unity project in this repo. Command shape: `Unity.exe -batchmode -nographics -projectPath <HOST_PROJECT> -runTests -testPlatform EditMode -testFilter <Name> -logFile -` (or Window > General > Test Runner). `<HOST_PROJECT>` must be supplied by the user.

## Review Focus

- Server JSON carries fields the project's class does not declare (e.g. `"mail"` deserialized into plain `UserData`): ignored, no exception.
- JSON missing fields: properties stay `null`/default (`Uid` null), no exception.
- `UpdateUsername` / `UpdateAvatar` called before any `GetUserData` (cache empty): no `NullReferenceException`, server call still made.
- Each mutation (fetch, update, `UpdateCachedUser`, `ClearData`, deactivate) raises `OnUserDataUpdated` exactly once; clear/deactivate raise it with `null`.
- Failed API response: exception message equals the response message (or `"An unknown error has occurred."` when empty) and the cache is untouched.

---

### Task 1: Fix test assembly references

The test asmdefs reference `Unity84b3bb65eed9dd219507.Apiservice`, which does not exist (runtime assembly is `Yatechstudio.Apiservice`), so no existing test can see package types. Nothing else in this plan is verifiable until this is fixed.

**Files:**
- Modify: `Tests/Editor/Yatechstudio.Apiservice.Editor.Tests.asmdef`
- Modify: `Tests/Runtime/Yatechstudio.Apiservice.Tests.asmdef`

**Interfaces:**
- Produces: EditMode test assembly that compiles against `Yatechstudio.Apiservice` and can use `Newtonsoft.Json` (needed by Task 2).

- [ ] **Step 1: Run the existing suite to see the failure**

Run the EditMode command with `-testFilter AuthApiServiceTests` in the host project.
Expected: compile error (assembly reference not found) or zero tests discovered.

- [ ] **Step 2: Edit both asmdefs**

In `references`, replace `Unity84b3bb65eed9dd219507.Apiservice` with `Yatechstudio.Apiservice`. In `precompiledReferences`, add `Newtonsoft.Json.dll`. Leave the asmdef `name` fields alone.

- [ ] **Step 3: Re-run `AuthApiServiceTests` and `ApiConfigTests`**

Expected: all pass (3 + existing ApiConfig tests). If they fail for a reason other than the reference, stop and report; do not fix unrelated production code.

- [ ] **Step 4: Commit**

```bash
git add Tests/Editor/Yatechstudio.Apiservice.Editor.Tests.asmdef Tests/Runtime/Yatechstudio.Apiservice.Tests.asmdef
git commit -m "fix(tests): reference the real runtime assembly in test asmdefs"
```

---

### Task 2: Generic user data (breaking)

**Files:**
- Modify: `Runtime/Domain/UserData/UserData.cs`
- Modify: `Runtime/Domain/API/Game/User/IUserApiService.cs`
- Modify: `Runtime/Infrastructure/API/Game/UserApiService.cs`
- Modify: `Runtime/Domain/API/Interface/IUserRepository.cs`
- Modify: `Runtime/Infrastructure/Repository/UserRepository.cs`
- Modify: `Runtime/Domain/API/Base/ApiResponseDto.cs:65-75` (delete top-level `Balance` and `BalanceEarn`; they are project data and unused in the package)
- Create: `Tests/Editor/Infrastructure/TestUser.cs`
- Create: `Tests/Editor/Infrastructure/UserDataTests.cs`
- Create: `Tests/Editor/Infrastructure/UserApiServiceTests.cs`
- Create: `Tests/Editor/Infrastructure/UserRepositoryTests.cs`

**Interfaces:**
- Consumes: `IGameApiService.GetAsync<T>(string url, CancellationToken ct)` / `PostAsync<T>(string url, object data, CancellationToken ct)`, `IApiConfig.API_BASE_URL`, `ApiResponseDto<T>` (`Status`, `Data`, `CreateError(string msg, int code, bool handled)`).
- Produces (Task 3 relies on these exact names):
  - `class UserData` with `Guid? Uid`, `string GameId`, `string Username`, `string RefCode`, `string AvatarId`, same `[JsonProperty]` names as today. Nested `Mail`/`Balance`/`BalanceEarn` and the `MailData`/`BalanceShop`/`BalanceGame` properties are removed.
  - `interface IUserApiService<TUser> where TUser : UserData`: `GetUserAsync`, `UpdateUsernameAsync`, `UpdateAvatarAsync` returning `UniTask<ApiResponseDto<TUser>>`; `DeactivateUserAsync` unchanged (`ApiResponseDto<object>`).
  - `class UserApiService<TUser> : IUserApiService<TUser>`, ctor `(IGameApiService apiService, IApiConfig config)`; URLs unchanged: `{API_BASE_URL}/users`, `/users/username`, `/users/avatar`, `/users/deactivate`.
  - `interface IUserRepository<TUser> where TUser : UserData` and `class UserRepository<TUser>`, ctor `(IUserApiService<TUser> userApiService)`; members identical to today with `UserData` replaced by `TUser` (`CurrentUser`, `GetUserData`, `UpdateAvatar`, `UpdateUsername`, `UpdateCachedUser(Action<TUser>)`, `event Action<TUser> OnUserDataUpdated`, `DeactivateUser`, `ClearData`).
  - Test helper `TestUser : UserData` with `[JsonProperty("mail")] TestMail Mail` where `TestMail` has `[JsonProperty("unreadCount")] int UnreadCount`.

- [ ] **Step 1: Write the failing tests**

`UserDataTests` (use `JsonConvert.DeserializeObject`), JSON `{"uid":"6f9619ff-8b86-d011-b42d-00cf4fc964ff","username":"bob","mail":{"unreadCount":3}}`:
  - `Deserialize_ProjectSubclass_ReadsExtraFields`: into `TestUser` → `Username == "bob"`, `Mail.UnreadCount == 3`.
  - `Deserialize_BaseType_IgnoresUnknownFields`: into `UserData` → no throw, `Username == "bob"`.
  - `Deserialize_MissingFields_LeavesDefaults`: `"{}"` → `Uid == null`, `Username == null`.

`UserApiServiceTests` (mock `IGameApiService`, config returns `https://mock-api.com/api`), each asserts `Received(1)` on the mock with `default` token:
  - `GetUserAsync_Calls_GetAsync_OnUsersUrl`: `GetAsync<TestUser>("https://mock-api.com/api/users", default)`.
  - `UpdateUsernameAsync_Posts_ToUsernameUrl`: `PostAsync<TestUser>("https://mock-api.com/api/users/username", Arg.Any<object>(), default)`.
  - `UpdateAvatarAsync_Posts_ToAvatarUrl`: same with `/users/avatar`.

`UserRepositoryTests` (mock `IUserApiService<TestUser>`; success response is `new ApiResponseDto<TestUser> { Status = "success", Data = user }`):
  - `GetUserData_FirstCall_FetchesCachesAndFiresEventOnce`: result is `user`, `CurrentUser` is `user`, event count `== 1`.
  - `GetUserData_SecondCall_UsesCache`: `GetUserAsync` `Received(1)` after two calls.
  - `GetUserData_ForceRefresh_Refetches`: `Received(2)` after `GetUserData()` then `GetUserData(true)`.
  - `GetUserData_Failure_ThrowsResponseMessage_AndCacheStaysNull`: response `ApiResponseDto<TestUser>.CreateError("boom", 500, false)` → `Exception` with `Message == "boom"`, `CurrentUser == null`.
  - `GetUserData_FailureWithEmptyMessage_ThrowsFallbackMessage`: `CreateError("", 500, false)` → message `"An unknown error has occurred."`.

- [ ] **Step 2: Run the new tests, confirm they fail**

Run EditMode with `-testFilter UserDataTests`, `UserApiServiceTests`, `UserRepositoryTests`.
Expected: compile errors (`UserApiService<>`, `IUserRepository<>` undefined).

- [ ] **Step 3: Implement the generic types and slim `UserData`**

Apply the signatures in Interfaces above to the five runtime files and delete `Balance`/`BalanceEarn` from `ApiResponseDto.cs`. Behavior of every method body stays exactly as today (Task 3 changes behavior); only types change.

- [ ] **Step 4: Run the three test classes plus `AuthApiServiceTests`, confirm pass**

Expected: all pass, no compile errors in the host project's `Library` log for `Yatechstudio.Apiservice`.

- [ ] **Step 5: Commit**

```bash
git add Runtime Tests
git commit -m "feat!: make user data generic so projects own their fields"
```

---

### Task 3: UserRepository behavior fixes

**Files:**
- Modify: `Runtime/Infrastructure/Repository/UserRepository.cs`
- Modify: `Tests/Editor/Infrastructure/UserRepositoryTests.cs`

**Interfaces:**
- Consumes: `UserRepository<TUser>` and `TestUser` from Task 2.
- Produces: no signature changes. Behavior contract: (a) one private `Notify()` raises `OnUserDataUpdated(_cachedUser)`; every mutation calls it exactly once; (b) update methods patch the cache only when it is non-null and return `_cachedUser` (so may return `null`); (c) `ClearData()` and successful `DeactivateUser` set the cache to `null` and notify.

- [ ] **Step 1: Add the failing tests**

Event counter incremented by a subscribed lambda; reset it after the initial load in tests that load first.
  - `UpdateUsername_AfterLoad_PatchesCacheAndFiresOnce`: `CurrentUser.Username == "new"`, count `== 1`.
  - `UpdateAvatar_AfterLoad_PatchesCacheAndFiresOnce`: same for `AvatarId`.
  - `UpdateUsername_BeforeLoad_DoesNotThrow_ReturnsNull_NoEvent`: API mock returns success; `UpdateUsernameAsync` `Received(1)`, result `null`, count `== 0`.
  - `UpdateUsername_Failure_ThrowsResponseMessage_CacheUnchanged`.
  - `ClearData_NullsCache_AndFiresEventWithNull`: subscriber receives `null`, count `== 1`.
  - `DeactivateUser_Success_ClearsCache_FiresOnceWithNull`.
  - `UpdateCachedUser_WhenEmpty_NoOpAndNoEvent`: action not invoked, count `== 0`.
  - `UpdateCachedUser_AfterLoad_AppliesActionAndFiresOnce`.

- [ ] **Step 2: Run `-testFilter UserRepositoryTests`, confirm the new tests fail**

Expected failures: `BeforeLoad` throws `NullReferenceException`; `AfterLoad` counts are 2; `ClearData` count is 0.

- [ ] **Step 3: Implement the contract above in `UserRepository<TUser>`**

Replace the `ChachedUser` property (typo, event-in-setter) with the `_cachedUser` field plus `Notify()`; delete `ForceRefreshUI`; drop the dead `?? "Failed to ..."` fallbacks (`ApiResponseDto.ErrorMessage` is never null). Keep `LoggerService` calls.

- [ ] **Step 4: Run all four test classes, confirm pass**

- [ ] **Step 5: Commit**

```bash
git add Runtime/Infrastructure/Repository/UserRepository.cs Tests/Editor/Infrastructure/UserRepositoryTests.cs
git commit -m "fix: null-safe UserRepository with single event per mutation"
```

---

### Task 4: Metadata, docs, sample, version

**Files:**
- Modify: `package.json`
- Modify: `README.md`
- Modify: `CHANGELOG.md`
- Delete: `Samples~/Example/SampleExample.cs` (package-template boilerplate; does not compile: undefined `MyPublicRuntimeExampleClass`, missing `using UnityEngine`)
- Create: `Samples~/Example/ExampleUserData.cs`

**Interfaces:**
- Consumes: `UserData`, `IUserApiService<TUser>`, `IUserRepository<TUser>`, `UserApiService<TUser>`, `UserRepository<TUser>` from Task 2.

- [ ] **Step 1: `package.json`**

Set `version` to `0.2.0`; replace "zero-allocation" with "low-allocation" in `description`; add `"com.unity.nuget.newtonsoft-json": "3.2.1"` to `dependencies`; update the sample `description` to "Project-specific UserData and VContainer registration". `com.unity.addressables` is unused in code: remove it only if none of the four GUIDs in `Runtime/Yatechstudio.Apiservice.asmdef` resolves (via `.meta` search in the host `Library/PackageCache`) to an Addressables assembly; otherwise keep it.

- [ ] **Step 2: Sample**

`ExampleUserData.cs` contains a `MyUserData : UserData` with `Mail`, `BalanceShop`, `BalanceGame` fields and their nested classes (moved from the old `UserData`), plus a short commented snippet of the two `builder.Register<...<MyUserData>>` lines.

- [ ] **Step 3: `README.md`**

Fix: add UPM git-URL install line (`...game-api-service.git#0.2.0`); add the two user registrations to the VContainer block; add a "Project-specific user data" section (subclass `UserData`, register closed generics, inject `IUserRepository<MyUserData>`); change "zero-allocation" to "low-allocation"; mention `ServiceInstaller` auto-installs UniTask, VContainer and Logger Service; replace "fully armed" testing claim with what exists (EditMode: config, auth, user service, user repository).

- [ ] **Step 4: `CHANGELOG.md`**

Add `## [0.2.0] - 2026-10-16` with **Breaking**: `UserData` fields `MailData`/`BalanceShop`/`BalanceGame` and top-level `Balance`/`BalanceEarn` removed (move to your own subclass); `IUserApiService`/`IUserRepository` and their implementations are now generic. **Fixed**: null-safe update methods, one event per mutation, `ClearData` raises the event, test asmdef references.

- [ ] **Step 5: Verify**

Run: `grep -rn "zero-allocation" README.md package.json` → no output.
In the host project: Package Manager > API Service > Samples > Import → no compile errors; rerun all four test classes → pass.

- [ ] **Step 6: Commit**

```bash
git add package.json README.md CHANGELOG.md Samples~
git commit -m "docs: v0.2.0 metadata, README, changelog, user data sample"
```

---

## Out of scope (own plans, need investigation first)

- `BaseHttpClient` hardening: refresh-token single-flight, recursive retry loop vs bounded loop, per-request timeout. Not read or verified yet.
- Namespacing package types (breaking, touches all files).
- Throw-on-failure vs returning `ApiResponseDto` so callers can honor `HandledByGlobalUI`.
- Replacing anonymous request bodies (`new { username }`) with DTOs.
- De-duplicating concurrent `GetUserData` calls.
