# Codebase Structure

## backend/src/ (Clean Architecture, deps flow inward: API -> Application -> Domain)
- `StarterKit.Domain` — entities (Account, ApiKey, AuditLog, StoredFile, SystemSetting,
  RefreshToken, EmailVerificationToken, ExternalLogin), `IRepository<T,TId>`, domain exceptions.
- `StarterKit.Application` — service interfaces/implementations under `Services/{Accounts,Auth,
  ApiKeys,AuditLogs,Files,SystemSettings}/`, DTOs, `Common/Settings` (options classes, incl.
  `RateLimiterSettings`), `Common/Mappings/EntityMapper.cs` (Mapperly), `Resources/` (Messages.resx
  vi/en, ApplicationMessages.cs).
- `StarterKit.Infrastructure` — EF Core (`Persistence/AppDbContext.cs`, `Configurations/`,
  `Repositories/`, single squashed `InitialCreate` migration); `Services/` split into per-concern
  subfolders — `Auth/` (incl. `Auth/External/` for Google/Microsoft providers), `Caching/`,
  `Context/` (clock/timezone/current-user), `Email/`, `Security/`, `Storage/` — each with its own
  `*Extensions` DI-registration class wired from `DependencyInjection.cs` (incl.
  `IGoogleJwtValidator`/`ISmtpClientFactory` — thin interfaces wrapping Google.Apis.Auth and
  MailKit's SmtpClient, added solely to make `GoogleAuthProvider`/`SmtpEmailSender` unit-testable).
- `StarterKit.API` — `Controllers/` (Accounts, ApiKeys, AuditLogs, Auth, Files, Health, Profile,
  SystemSettings), `Middleware/` (exception handling, user-timezone), `Program.cs`, OpenAPI/Scalar.

## backend/tests/ (all xUnit)
- `StarterKit.Domain.Tests` — plain unit tests, no mocking lib, entity coverage.
- `StarterKit.Application.Tests` — NSubstitute mocks; every service now covered (Auth + Accounts +
  ApiKeys + AuditLogs + Files + SystemSettings). `TestSupport/` has `ApplicationAssert` (exception
  assertion helpers) and `RepositoryPredicateStub` (fake `FirstOrDefaultAsync` predicate evaluation
  against a seed list).
- `StarterKit.Infrastructure.Tests` — unit tests for JWT/password/cache/storage/timezone/
  current-user/Google/SMTP services (NSubstitute; internal types need
  `InternalsVisibleTo("DynamicProxyGenAssembly2")` on the src csproj for NSubstitute/Castle
  DynamicProxy to mock them, in addition to the usual test-assembly grant), plus
  Testcontainers.PostgreSql-backed tests (`TestSupport/PostgresContainerFixture.cs`, one container
  shared per assembly via an xUnit collection fixture, migrated once, each test opens its own
  transaction and rolls back on dispose) for `GenericRepository`/`AuditLogRepository` (both need a
  real Postgres — `EF.Functions.ILike` and `jsonb` aren't supported by EF InMemory) and
  `RefreshTokenCleanupService`.
- `StarterKit.API.Tests` — `WebApplicationFactory<Program>` + one shared Testcontainers Postgres
  container per assembly + Respawn-based per-test reset (`TestSupport/ApiFactoryFixture.cs`).
  Config overrides (connection string, `RateLimiterSettings`, `StorageSettings:BasePath`) go via
  process environment variables set before first touching `Server` — `ConfigureWebHost`'s
  `ConfigureAppConfiguration` does NOT reliably out-prioritize `appsettings.json` for this
  minimal-API `Program.cs`. `ConfigureTestServices` (DI-level swaps, e.g. `IEmailSender` ->
  no-op) works reliably and is used for that. Requires Docker locally/in CI.

## frontend/src/
- `api/` — `{resource}-api.ts` modules + `client.ts` (axios wrapper, 401 refresh queue) + `types.ts`
  (hand-maintained, not codegen'd).
- `stores/` — `auth.ts`, `locale-store.ts`, `sidebar-store.ts` (Pinia setup-stores).
- `views/` — `HomeView`, `AccountsView`, `LoginView`, `RegisterView`, `VerifyEmailView`, `NotFoundView`.
- `components/`, `composables/`, `layouts/`, `locales/{vi,en}.ts`, `router/`.
- `lib/feedback.ts` / `feedback-naive.ts` — generic UI toast/confirm-dialog utility (name is a
  coincidence with the old "FeedbackHub" product name, unrelated to it).

## shared/
- `docs/` — currently near-empty (product-specific docs removed during the StarterKit rename).
- `openapi/` — placeholder for backend's exported OpenAPI spec; no codegen pipeline wired up yet
  (frontend's `api/types.ts` is hand-maintained — see `.claude/rules/api-contract.md`).

## Root
- `docker-compose.yml` — Postgres + Mailpit for local dev.
- `plans/` — historical dev-log plan files, reference only.
- `.claude/decisions.md` — non-obvious architectural decisions still relevant to this baseline.
