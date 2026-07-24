# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added

- Full backend test coverage across all layers: `StarterKit.Application.Tests` now covers every service (Account, ApiKey, AuditLog, File, SystemSettings, in addition to the existing Auth coverage); new `StarterKit.Infrastructure.Tests` project covers JWT/password/cache/storage/timezone/Google/SMTP services plus Testcontainers.PostgreSql-backed repository and cleanup-service tests; new `StarterKit.API.Tests` project adds `WebApplicationFactory` + Testcontainers integration tests across all 8 controllers and cross-cutting middleware.
- `RateLimiterSettings` config seam for `AuthController`'s rate limiter (production default unchanged).

### Changed

- `GoogleAuthProvider` and `SmtpEmailSender` refactored behind thin interfaces (`IGoogleJwtValidator`, `ISmtpClientFactory`) to enable unit testing — no behavior change.

### StarterKit baseline

This repo was repurposed from a product-specific codebase into a generic app starter. Baseline state:

- Multi-tenancy (Tenant/TenantMembership/TenantRole, `X-Tenant-Id`) removed — single-user/single-account model.
- Kept: email+password auth with mandatory email verification, Google social login, session management, Account/Profile, ApiKey, AuditLog, File storage, SystemSettings.
- EF Core migrations squashed to a single `InitialCreate` migration.
- Project renamed (namespaces, solution, packages) to `StarterKit`.
- Added `docker-compose.yml` (Postgres + Mailpit) for local dev bootstrap.

See `.claude/decisions.md` for the architectural decisions still relevant to this baseline (auth/session design, error envelope shape, localization approach, etc.).
