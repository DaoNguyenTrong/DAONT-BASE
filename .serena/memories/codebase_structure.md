# Codebase Structure

## backend/src/ (Clean Architecture, deps flow inward: API -> Application -> Domain)
- `StarterKit.Domain` — entities (Account, ApiKey, AuditLog, StoredFile, SystemSetting,
  RefreshToken, EmailVerificationToken, ExternalLogin), `IRepository<T,TId>`, domain exceptions.
- `StarterKit.Application` — service interfaces/implementations under `Services/{Accounts,Auth,
  ApiKeys,AuditLogs,Files,SystemSettings}/`, DTOs, `Common/Settings` (options classes),
  `Common/Mappings/EntityMapper.cs` (Mapperly), `Resources/` (Messages.resx vi/en, ApplicationMessages.cs).
- `StarterKit.Infrastructure` — EF Core (`Persistence/AppDbContext.cs`, `Configurations/`,
  `Repositories/`, single squashed `InitialCreate` migration), auth/email/storage service impls
  under `Services/`.
- `StarterKit.API` — `Controllers/` (Accounts, ApiKeys, AuditLogs, Auth, Files, Health, Profile,
  SystemSettings), `Middleware/` (exception handling, user-timezone), `Program.cs`, OpenAPI/Scalar.

## backend/tests/
- `StarterKit.Domain.Tests`, `StarterKit.Application.Tests` — xUnit + NSubstitute, mirrors src/ split.

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
