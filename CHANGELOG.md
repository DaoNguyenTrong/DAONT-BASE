# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Security

- CORS previously reflected any Origin with credentials enabled (equivalent to a wildcard, but worse); now requires an explicit `CorsSettings:AllowedOrigins` allowlist, configured at startup.
- Rate limiting keyed on `Connection.RemoteIpAddress` with no forwarded-headers handling — behind any reverse proxy/load balancer every client collapsed into a single bucket. Added opt-in `ForwardedHeadersSettings` (`KnownProxies`/`KnownNetworks`), defaulting to ASP.NET Core's loopback-only trust so it's a no-op until an operator explicitly configures their proxy.

### Added

- Microsoft sign-in (MSAL.js popup + PKCE) alongside the existing Google option, backed by a new `MicrosoftAuthProvider`/`IMicrosoftJwtValidator` on the backend's provider-agnostic external-login flow.
- Backend exports its OpenAPI spec to `shared/openapi/openapi.json` at build time (`dotnet build ... -p:OpenApiGenerateDocumentsOnBuild=true`, off by default to avoid slowing every plain build), with operation transformers for stable OperationIds, a single `application/json` request content-type, and camelCase query parameter names.
- Frontend generates its API client from that spec via `orval` (`bun run --cwd frontend codegen`; also runs automatically on `bun install` via `postinstall`) — the OpenAPI contract between backend and frontend is no longer hand-synced.
- `RENAMING.md` — checklist for renaming the project (namespaces, solution/project files, config values) with the pitfalls to avoid (DB name, JWT issuer/audiences).
- Weatherplus branding: primary/surface/success color palette derived from the logo, wordmark/mark logo assets used across the sidebar and auth pages, and a regenerated favicon/PWA icon set (WebP where the format allows it).

### Changed

- Restyled the Google sign-in button to a full-width text+logo button matching Microsoft's, extracting the shared "Or continue with" divider into `SocialLoginDivider`.
- Normalized 4 controller routes (`Accounts`, `Auth`, `Files`, `Health`) from `[Route("api/[controller]")]` to explicit lowercase (`api/accounts`, etc.) — ASP.NET Core routing was always case-insensitive, but the OpenAPI spec captures the declared casing verbatim and codegen reproduced it, so this keeps the generated client's paths consistent with every existing caller.
- Removed the hand-written `frontend/src/api/{account,auth,health,profile}-api.ts` wrapper modules; views/composables now call the generated client directly. Side effects the OpenAPI spec can't express (updating auth state after login, session-id normalization, the refresh-token client isolation) moved into `stores/auth.ts` as actions.
- Deduplicated the refresh-token call between the axios 401-retry interceptor and the auth store — the interceptor now delegates to `authStore.refreshToken()` instead of reimplementing it.
- Dark mode body/card switched to explicit navy hex values (`#121527`/`#1e2235`); retinted the surface-600/700/800 hover/border/fill tokens and boosted the dark-mode primary to a more saturated violet (`#9c80dc`) so both stay visually consistent with the new navy base.

### Fixed

- The 401 interceptor's refresh-failure path now also clears the "keep me logged in" preference, matching `logout()`'s behavior (previously only cleared on the next app boot).
- `.claude/rules/authentication.md` described a multi-tenancy system (`TenantRole`, `X-Tenant-Id`, `ICurrentTenantService`) that doesn't exist anywhere in the codebase and contradicted the README; corrected to match actual auth behavior.

## [v1.0.0] - 2026-07-24

### Security

- Bump Microsoft packages from 10.0.8 to 10.0.10 to resolve five high-severity DoS advisories (GHSA-23rf-6693-g89p and related) on transitive `System.Security.Cryptography.Xml`.

### Added

- Full backend test coverage across all layers: `StarterKit.Application.Tests` now covers every service (Account, ApiKey, AuditLog, File, SystemSettings, in addition to the existing Auth coverage); new `StarterKit.Infrastructure.Tests` project covers JWT/password/cache/storage/timezone/Google/SMTP services plus Testcontainers.PostgreSql-backed repository and cleanup-service tests; new `StarterKit.API.Tests` project adds `WebApplicationFactory` + Testcontainers integration tests across all 8 controllers and cross-cutting middleware.
- `RateLimiterSettings` config seam for `AuthController`'s rate limiter (production default unchanged).
- Additional backend test coverage from a follow-up review: expired/tampered JWT rejection, oversized file upload, DataAnnotations validation-400 paths on Accounts/ApiKeys/Profile, auth cookie flag assertions (`HttpOnly`/`SameSite`), `RefreshTokenCleanupService`'s failure-swallowing path, `UserTimeZoneProvider`'s `httpContext.Items` string-id branch, and expanded `Account`/`ApiKey` domain entity cases.
- Frontend test coverage from a test-gap audit: `AccountsView.vue` (create/edit/delete dialogs, debounced search, infinite scroll — previously untested), concurrent-401 refresh queueing in the api client (`isRefreshing`/`failedQueue`), and unit tests for the router's auth/guestOnly guard.
- `@vitest/coverage-v8` wired up for the frontend (`bun run test:coverage`) to measure real line/branch coverage instead of file-presence heuristics.
- Frontend test coverage for the app shell layout (`AppHeader`, `AppFooter`, `AppSidebar`, `AppSidebarItem`, `AppLayout`), previously entirely untested despite rendering on every authenticated page; also fills out `AccountForm`'s remaining untested field bindings.

### Fixed

- `GenericRepositoryTests`' `CreatedAt`-ordering test was non-deterministic (audit-field stamping silently overwrote manually-seeded timestamps); fixed to seed a genuine timestamp difference.
- `AccountServiceTests`' email-collision test used an over-permissive mock that never evaluated the self-exclusion predicate — tightened so the check is actually exercised.
- Admin account creation (`POST /api/accounts`) returned a generic 500 on a duplicate username/email instead of a 409 Conflict; now pre-checks uniqueness like registration does.

### Changed

- `GoogleAuthProvider` and `SmtpEmailSender` refactored behind thin interfaces (`IGoogleJwtValidator`, `ISmtpClientFactory`) to enable unit testing — no behavior change.
- Frontend router's `beforeEach` guard extracted into an exported `resolveGuardRedirect` function to enable direct unit testing — no behavior change.
- README translated to English.
- `.claude/decisions.md` convention tightened to record only the *why* (no restating what/how), with a lower per-entry word cap; prior entries cleared.

### StarterKit baseline

This repo was repurposed from a product-specific codebase into a generic app starter. Baseline state:

- Multi-tenancy (Tenant/TenantMembership/TenantRole, `X-Tenant-Id`) removed — single-user/single-account model.
- Kept: email+password auth with mandatory email verification, Google social login, session management, Account/Profile, ApiKey, AuditLog, File storage, SystemSettings.
- EF Core migrations squashed to a single `InitialCreate` migration.
- Project renamed (namespaces, solution, packages) to `StarterKit`.
- Added `docker-compose.yml` (Postgres + Mailpit) for local dev bootstrap.

See `.claude/decisions.md` for the architectural decisions still relevant to this baseline (auth/session design, error envelope shape, localization approach, etc.).
