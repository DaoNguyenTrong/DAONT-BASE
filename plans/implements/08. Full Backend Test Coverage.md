# Full Backend Test Coverage — StarterKit

## Context

Backend test coverage today: `StarterKit.Domain.Tests` fully covers entities; `StarterKit.Application.Tests` has exactly one tested service (`AuthServiceTests`, 41 cases) out of six; there is **no** `StarterKit.Infrastructure.Tests` or `StarterKit.API.Tests` project, so every Infrastructure class (repositories, JWT, password hashing, storage, email, Google auth) and every API controller has zero test coverage. The goal is to bring the backend to full test coverage across all three layers.

User-approved scope decisions (do not re-litigate):
1. Cover all three layers: Application, Infrastructure, API.
2. Use **Testcontainers.PostgreSql** for anything needing a real Postgres (not EF InMemory — incompatible, since `GenericRepository`/`AuditLogRepository` use Npgsql-only `EF.Functions.ILike` and `AuditLogConfiguration` maps `jsonb` columns; not the existing docker-compose Postgres — shared/stateful, not test-isolated).
3. Refactor `GoogleAuthProvider` and `SmtpEmailSender` behind thin new interfaces so their external calls become mockable.
4. Add a small `RateLimiterSettings` config seam so AuthController integration tests can raise the hardcoded 5-req/min limit without changing production behavior (default stays 5/min).
5. Change `RefreshTokenCleanupService.RunCleanupAsync` from `private` to `internal` (an `InternalsVisibleTo("StarterKit.Infrastructure.Tests")` already exists on that project) so its delete logic is directly testable without reflection.
6. **Execute in three checkpointed phases** — Application → Infrastructure → API — running the full suite and reporting back after each phase before starting the next.

Existing test conventions to follow throughout (from `backend/tests/StarterKit.Application.Tests/Services/Auth/AuthServiceTests.cs`, `TestSupport/ApplicationAssert.cs`, `TestSupport/RepositoryPredicateStub.cs`):
- Private `Fixture` record bundling the service-under-test + NSubstitute mocks of its dependencies, built by a `CreateFixture()` factory; `IUnitOfWork.Repository<T,TId>()` wired to return the mock repo.
- Entity-building helpers call real domain `Create()` factories, never construct entities by hand.
- `RepositoryPredicateStub.StubFirstOrDefault(repo, seedList)` when `FirstOrDefaultAsync` needs to evaluate a real LINQ predicate against a seed list.
- `ApplicationAssert.ThrowsWithMessageAsync<TException>(msg, act)` / `AssertNotFoundAsync<TEntity>(id, act)` for exception assertions.
- Tests assert both return values and `Received(1)`/`DidNotReceive()` mock-call verification; group test methods by `// MethodName` comment sections.

---

## Phase 1 — Application services (`StarterKit.Application.Tests`)

No new project/package needed. Add one test class per service, following the `AuthServiceTests` pattern exactly.

**TestSupport change**: extend `backend/tests/StarterKit.Application.Tests/TestSupport/RepositoryPredicateStub.cs` with an `IRepository<T, int>` overload (needed for `SystemSetting : BaseEntity` which is int-keyed; `IRepository<T>` already extends `IRepository<T,int>` so no separate overload is needed beyond that).

### `Services/Accounts/AccountServiceTests.cs`
Fixture: `IUnitOfWork`+`IRepository<Account,Guid>`, `ICurrentUserService`, `IPasswordHasher`.
- `GetAllAsync` — page/size defaulting (`<1`→default), search trim/null, maps via `ListPagedAsync` with 3 search columns.
- `GetByIdAsync` — found/not-found (`AssertNotFoundAsync<Account>`).
- `CreateAsync` — hashes password, `AddAsync`+`SaveChangesAsync` once.
- `UpdateAsync` — not-found throws; found updates + saves.
- `DeleteAsync` — not-found throws, `Delete` never called; found deletes + saves.
- `GetCurrentProfileAsync` — unparsable/null `UserId` → `UnauthorizedException`; account missing → same; valid → `ProfileDto`.
- `UpdateCurrentProfileAsync` — email collision with a different account → `ConflictException`, no update; own email unchanged → succeeds; `Username`/`Status` preserved across the update.
- `ChangePasswordAsync` — null/blank hash → `UnauthorizedException`, `Verify` never called; `Verify` false → `UnauthorizedException`; success → hash+update+save once.

### `Services/ApiKeys/ApiKeyServiceTests.cs`
Fixture: `IUnitOfWork`+`IRepository<ApiKey,Guid>`.
- `CreateAsync` — raw key matches `^sk_[A-Za-z0-9_-]+$`; `AddAsync`+`SaveChangesAsync` once; result DTO matches created entity.
- `GetAllAsync` — empty list; ordered by `CreatedAt` descending (seed out of order, set `CreatedAt` explicitly).
- `DeactivateAsync` — not-found (`AssertNotFoundAsync<ApiKey>`); found → `IsActive=false`, update+save.

### `Services/AuditLogs/AuditLogServiceTests.cs`
Fixture: `IAuditLogRepository` only (no `IUnitOfWork` — this service doesn't take one).
- `GetByIdAsync` throws `NotFoundException("AuditLog", id)` — a **string literal**, not `nameof()` (the `AuditLog` type lives in Infrastructure, unreachable here). Assert `ex.Args[0]=="AuditLog"`/`ex.Args[1]==id` directly, or add a small `ApplicationAssert.AssertNotFoundAsync(string entityName, object id, act)` overload.
- `GetAllAsync` — page/size defaulting; passes `search`(trimmed/null)/`userId`/`systemOnly` straight through; wraps into `PagedResult`.

### `Services/Files/FileServiceTests.cs`
Fixture: `IStorageService`, `IUnitOfWork`+`IRepository<StoredFile,Guid>`, `IOptions<StorageSettings>`.
- `UploadAsync` — `Size<=0`→`DomainException(FileIsRequired)`, storage never called; `Size>Max`→`FormattedDomainException(FileSizeExceeded, max)`; disallowed content-type→`DomainException(FileContentTypeNotAllowed)`; empty `AllowedContentTypes`→any type passes; **persisted `Size` comes from `uploadResult.Size`, not `request.Size`** (assert with mismatched values); `Url` built as `PublicUrlBase.TrimEnd('/') + "/" + storagePath.TrimStart('/')`.
- `GetByIdAsync`/`GetAllAsync` — not-found; page/size defaulting (note: this list overload has no search predicate).
- `DownloadAsync` — not-found; found calls `storageService.DownloadAsync(storedFile.StoragePath, ...)`.
- `DeleteAsync` — not-found, storage delete never called; found — **storage delete happens before DB delete** (assert via `Received.InOrder`), then save once.

### `Services/SystemSettings/SystemSettingsServiceTests.cs`
Fixture: `IUnitOfWork`+`IRepository<SystemSetting>` (int-keyed), `ICacheService` stubbed so `GetOrSetAsync` invokes its factory:
```csharp
cacheService.GetOrSetAsync(Arg.Any<string>(), Arg.Any<Func<CancellationToken, Task<IReadOnlyDictionary<string,string?>>>>(), Arg.Any<TimeSpan?>(), Arg.Any<CancellationToken>())
    .Returns(ci => ci.Arg<Func<CancellationToken, Task<IReadOnlyDictionary<string,string?>>>>()(CancellationToken.None));
```
- `GetAllAsync` — factory invoked, `ListAsync(_ => true, ct)` called, returns `Key`→`Value` dict including nulls.
- `UpdateSectionAsync` — mixed create+update in one call (one key exists, one is new); **exactly one** `SaveChangesAsync` and **exactly one** `RemoveAsync` regardless of dict size, `RemoveAsync` called *after* `SaveChangesAsync` (`Received.InOrder`). Note: current code calls save+invalidate even for an empty dict — test to match actual behavior, don't "fix" it here.

**Checkpoint**: run `dotnet test backend/StarterKit.sln --no-restore -m:1`, confirm all green, report count of new tests before moving to Phase 2.

---

## Phase 2 — Infrastructure (`StarterKit.Infrastructure.Tests`, new project)

### 2.0 Refactors (ship with their own unit tests in the same change)

**`GoogleAuthProvider` → `IGoogleJwtValidator`** — new `backend/src/StarterKit.Infrastructure/Services/IGoogleJwtValidator.cs`:
```csharp
internal interface IGoogleJwtValidator
{
    Task<GoogleJsonWebSignature.Payload> ValidateAsync(string credential, GoogleJsonWebSignature.ValidationSettings settings);
}
internal sealed class GoogleJwtValidator : IGoogleJwtValidator
{
    public Task<GoogleJsonWebSignature.Payload> ValidateAsync(string credential, GoogleJsonWebSignature.ValidationSettings settings)
        => GoogleJsonWebSignature.ValidateAsync(credential, settings);
}
```
`GoogleAuthProvider` takes `IGoogleJwtValidator` in its constructor and calls it instead of the static method; the `try/catch (InvalidJwtException) → UnauthorizedException` mapping stays as-is. Register in `ExternalAuthExtensions.cs` next to the existing `IExternalAuthProvider` registration (inside the same `ClientId` guard).

**`SmtpEmailSender` → `ISmtpClientFactory`** — MailKit's `SmtpClient` already implements the public `MailKit.Net.Smtp.ISmtpClient`, so only a factory is needed. New `backend/src/StarterKit.Infrastructure/Services/ISmtpClientFactory.cs`:
```csharp
internal interface ISmtpClientFactory { ISmtpClient Create(); }
internal sealed class SmtpClientFactory : ISmtpClientFactory { public ISmtpClient Create() => new SmtpClient(); }
```
`SmtpEmailSender` takes `ISmtpClientFactory`, replaces `new SmtpClient()` with `smtpClientFactory.Create()`. Register in `EmailExtensions.cs` alongside `IEmailSender`.

**`RefreshTokenCleanupService.RunCleanupAsync`**: change access modifier `private` → `internal`. No logic change.

### 2.1 New project
`backend/tests/StarterKit.Infrastructure.Tests/StarterKit.Infrastructure.Tests.csproj` — mirror `Domain.Tests`/`Application.Tests` shape (`net10.0`, `ImplicitUsings`, `Nullable`, `IsPackable=false`, `<Using Include="Xunit"/>`):
```xml
<PackageReference Include="coverlet.collector" Version="6.0.4" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
<PackageReference Include="NSubstitute" Version="6.0.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.13.0" />
```
Project references: `StarterKit.Infrastructure`, `StarterKit.Application`, `StarterKit.Domain`. Add to `StarterKit.sln`.

### 2.2 Pure/unit-testable classes (no Docker)
- **`JwtTokenServiceTests`**: `GenerateAccessToken` — throws when `Audiences` empty; produces a token with correct claims/issuer/audience/expiry (tolerance-based, like `AuthServiceTests`). `GenerateRefreshToken` — URL-safe base64, no padding, two calls differ.
- **`PasswordHasherTests`** (keep small — BCrypt work factor 12 is slow): hash/verify round-trip true and false; two hashes of the same password differ (salted).
- **`MemoryCacheServiceTests`** (real `MemoryCache` + real `IOptions<CacheSettings>`, not mocked): get/set round-trip; `GetOrSetAsync` invokes factory only on miss; `RemoveAsync` evicts; `RemoveByPrefixAsync` removes only matching keys.
- **`DateTimeProviderTests`**: single smoke test, `UtcNow` within a few seconds of real `DateTime.UtcNow`, `Kind==Utc`.
- **`UserTimeZoneProviderTests`** (`IHttpContextAccessor`+`DefaultHttpContext`): no context→UTC; valid `X-TimeZone` header resolves; invalid header falls back to UTC; `ConvertToUtc`/`ConvertFromUtc` round-trip for a non-UTC zone; value memoized after first access.
- **`CurrentUserServiceTests`** (`DefaultHttpContext`+`ClaimsPrincipal`): no user→nulls/false; claims map to `UserId`/`UserName`; API-key claim present/absent/non-Guid.
- **`DataProtectionSecretProtectorTests`** (real ephemeral `DataProtectionProvider.Create(...)`): protect/unprotect round-trip; tampered ciphertext throws.
- **`GoogleAuthProviderTests`** (post-refactor, substitute `IGoogleJwtValidator`): payload maps to `ExternalUserInfo`; `InvalidJwtException`→`UnauthorizedException`; configured `ClientId` passed as audience.
- **`SmtpEmailSenderTests`** (post-refactor, substitute `ISmtpClientFactory`→substitute `ISmtpClient`): connects with configured host/port/ssl; auth skipped when username blank; correct `MimeMessage` sent; disconnect called in `finally` even on send failure.
- **`Storage/StorageServiceTests`** (substitute `IStorageProvider`): all 4 methods are pure 1:1 delegation — one test each.
- **`Storage/StoragePathGeneratorTests`**: date-segmented path, forward-slash separators, extension preserved, two calls differ.
- **`Storage/LocalFileProviderTests`** (real temp dir via `Directory.CreateTempSubdirectory()`, fake `IHostEnvironment`, real `StoragePathGenerator`): upload writes real file with correct size; download missing→`NotFoundException`; delete missing is no-op, delete existing removes it; **path-traversal guard** — a crafted `storagePath` resolving outside `BasePath` throws (reached via `DownloadAsync`/`DeleteAsync`, since the guard is a private helper).

### 2.3 Testcontainers-backed classes
Shared fixture `TestSupport/PostgresContainerFixture.cs` (one Postgres 16-alpine container per test **assembly**, via `IAsyncLifetime` + `ICollectionFixture`), migrated once via a standalone `AppDbContext` before any test runs. `[Collection(nameof(PostgresCollection))]` on the 3 classes below. Each test wraps its own work in a transaction (`BeginTransaction`/`UseTransaction`, rollback in per-test-class `IAsyncLifetime.DisposeAsync`) for isolation — no Respawn needed here since these tests talk to `AppDbContext` directly, not through a hosted app.

- **`Persistence/Repositories/GenericRepositoryTests`**: `GetByIdAsync`/`FirstOrDefaultAsync`/`ListAsync` (both overloads); `ListPagedAsync(page,size)` ordering+count; `ListPagedAsync(predicate,page,size)`; **`ListPagedAsync(predicate,searchTerm,searchColumns,page,size)` exercising the Npgsql-only `ILike` path** (case-insensitive partial match, multi-column, empty term skips filter) — this is the core reason this class needs Testcontainers; `AddAsync`/`Update`/`Delete` persist correctly.
- **`Persistence/Repositories/AuditLogRepositoryTests`**: `ILike` search across entity/action/user columns including null-`UserId` (system) rows; `userId`/`systemOnly` filters; left-join to `Accounts` populates/omits `UserName` correctly; ordering/paging.
- **`Services/RefreshTokenCleanupServiceTests`** (now `internal RunCleanupAsync` is callable directly): seed expired, revoked-and-old, still-valid, and revoked-but-recent tokens; after cleanup only the first two are deleted; cutoff math from `IOptions<RefreshTokenCleanupSettings>`.

**Checkpoint**: run `dotnet test backend/StarterKit.sln --no-restore -m:1` (Docker must be running locally), confirm all green, report before Phase 3.

---

## Phase 3 — API controllers (`StarterKit.API.Tests`, new project)

### 3.0 Production seam
Add `RateLimiterSettings` (Application `Common/Settings`, `AuthPermitLimit=5`, `AuthWindowMinutes=1` defaults) and wire it into the `"auth"` rate-limiter policy in `Program.cs`, reading from configuration instead of hardcoded literals. No `appsettings.json` entry needed (defaults preserve current production behavior); the test host overrides `RateLimiterSettings:AuthPermitLimit` to a high value via `ConfigureAppConfiguration`.

Also add `InternalsVisibleTo("StarterKit.API.Tests")` to `StarterKit.API.csproj` (`Program` is `internal` via top-level statements — same pattern already used on `StarterKit.Infrastructure.csproj`).

### 3.1 New project
`backend/tests/StarterKit.API.Tests/StarterKit.API.Tests.csproj`:
```xml
<PackageReference Include="coverlet.collector" Version="6.0.4" />
<PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.7.0" />
<PackageReference Include="NSubstitute" Version="6.0.0" />
<PackageReference Include="xunit" Version="2.9.3" />
<PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
<PackageReference Include="Testcontainers.PostgreSql" Version="4.13.0" />
<PackageReference Include="Microsoft.AspNetCore.Mvc.Testing" Version="10.0.8" />
<PackageReference Include="Respawn" Version="7.0.0" />
```
Project references: `StarterKit.API`, `StarterKit.Application`, `StarterKit.Infrastructure`, `StarterKit.Domain`. Add to `StarterKit.sln`.

### 3.2 Shared fixture — `TestSupport/ApiFactoryFixture.cs`
`WebApplicationFactory<Program>` + `IAsyncLifetime`: starts a Postgres 16-alpine container, migrates via a standalone `AppDbContext` **before** the host builds (avoids racing the unconditional `SystemSettingSeeder` on host startup), sets up Respawn (excluding `SystemSettings`, `DataProtectionKeys`, `__EFMigrationsHistory` — startup-seeded/global, not per-test state). `ConfigureWebHost` overrides `ConnectionStrings:DefaultConnection` and `RateLimiterSettings:AuthPermitLimit`, and removes the `RefreshTokenCleanupService` hosted service (avoids background-scope disposal races during test teardown). One container shared per assembly via `[CollectionDefinition]`/`[ICollectionFixture]`; reset business tables via Respawn after each test class.

Known constraint: auth cookies are `Secure=true; SameSite=None` and won't round-trip through `WebApplicationFactory`'s plain-HTTP `HttpClient`/`CookieContainer`. Tests assert `Set-Cookie` response headers directly and drive authenticated flows via the JSON body tokens instead.

`TestSupport/AuthTestHelper.cs`: seeds an `Account` (and optionally an `ApiKey`) directly via `AppDbContext`, mints a real JWT through the actual `JwtTokenService`/settings so the real bearer pipeline validates it (no auth mocking). All test clients get a default `X-TimeZone: UTC` header.

### 3.3 Per-controller scenarios
- **HealthController**: 200 with expected shape; succeeds even *without* `X-TimeZone` (proves the middleware's explicit exemption).
- **Cross-cutting middleware**: missing `X-TimeZone` on any other route → 400 before auth runs; invalid timezone id → 400; `OPTIONS` bypasses the check.
- **AuthController** (heaviest — rate limit raised via seam): register (success/duplicate/validation), login (unconfirmed/wrong-password/success incl. `Set-Cookie` header assertions), verify-email (valid/expired/consumed), resend-verification (unknown-silent/already-confirmed), external login (unsupported provider → 400; substitute `IExternalAuthProvider`/`IGoogleJwtValidator` via `ConfigureTestServices` for this flow only), refresh (via body token), logout, get/revoke sessions, revoke-others, one `X-Api-Key`-authenticated request against an `[Authorize]` endpoint. One separate low-volume test class re-instates the production default (`AuthPermitLimit=5`) to prove a 6th rapid login returns 429.
- **AccountsController**: list with paging+search (end-to-end `ILike` proof through the real stack), get/create/update/delete incl. 404/409/400/401 branches.
- **ProfileController**: get/update own profile (incl. email-collision 409), change-password (wrong-current 401, success 204, validation 400).
- **ApiKeysController**: list ordered desc, create (raw key format), deactivate; deactivated key no longer authenticates on another endpoint.
- **AuditLogsController**: perform a real mutating call (e.g. `POST /accounts`) and assert an audit row appears, then list with `userId`/`systemOnly` filters, get-by-id found/404.
- **FilesController**: multipart upload (real bytes, correct persisted size, `StorageSettings:BasePath` overridden to a per-test-run temp dir — never write into the repo's `uploads/`), oversized/disallowed-type rejections (via config override), get/download/delete incl. re-delete → 404.
- **SystemSettingsController**: get reflects seeded defaults (proves `SystemSettingSeeder` ran); update-section creates+updates in one call, subsequent get proves cache invalidation.

**Checkpoint**: run `dotnet test backend/StarterKit.sln --no-restore -m:1` (Docker required), confirm all green, report final counts across all three phases.

---

## Risks

| Risk | Mitigation |
|---|---|
| Shared container ≠ clean state between tests | Transaction-rollback for direct-`AppDbContext` Infrastructure tests; Respawn (excluding seed/migration tables) for API tests |
| `SystemSettingSeeder` races the host boot | Fixture migrates via a standalone `AppDbContext` before constructing `WebApplicationFactory` |
| Hardcoded 5-req/min rate limiter blocks AuthController tests | New `RateLimiterSettings` seam, default unchanged in production |
| Secure/SameSite=None cookies don't survive plain-HTTP `TestServer` | Assert `Set-Cookie` headers directly; drive flows via JSON body tokens |
| BCrypt work factor 12 is slow | Keep `PasswordHasherTests` small; seed test accounts with a pre-computed hash rather than calling `Hash` in every fixture |
| `RefreshTokenCleanupService` hosted service could race during API test teardown | Removed from DI in `ApiFactoryFixture.ConfigureWebHost` |
| Container startup cost (paid twice: Infrastructure.Tests + API.Tests) | Acceptable — one container per assembly via collection fixture, not per class/test |
| No PR-gating CI today (`release.yml` only runs on `v*` tags) | Out of scope for this plan; new Docker-dependent suite won't run until release time unless a separate CI workflow is added later |
| Multipart upload writes real files | `StorageSettings:BasePath` overridden to a per-test-run temp dir, cleaned up in fixture teardown |

## Verification (after each phase)

```bash
dotnet build backend/StarterKit.sln --no-restore -m:1
dotnet test backend/StarterKit.sln --no-restore -m:1
```
Phases 2 and 3 require Docker running locally (Testcontainers). After Phase 3, also confirm `backend/StarterKit.sln` lists all 5 test projects and CI's `release.yml` step (`dotnet test`) still passes conceptually (same command).

## Decision log entries to add (`.claude/decisions.md`)
- Testcontainers.PostgreSql adopted as the integration-test harness (over EF InMemory/existing docker-compose) — reason: `ILike`/`jsonb` usage makes InMemory non-viable; docker-compose isn't test-isolated.
- `GoogleAuthProvider`/`SmtpEmailSender` refactored behind thin interfaces (`IGoogleJwtValidator`, `ISmtpClientFactory`) solely to enable unit testing, no behavior change.
- `RateLimiterSettings` config seam added for AuthController testability, production default unchanged.
