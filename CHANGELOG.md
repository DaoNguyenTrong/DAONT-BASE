# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

### Added

- Optional SeaweedFS file storage via its S3 gateway, with a local docker-compose service for development.
- Cache provider selectable via config (keyed DI), matching the existing storage-provider pattern.

### Fixed

- Email verification links now work with the app's hash-based routing — previously the link bounced unauthenticated users to the login page instead of the verification screen.
- A temporary email-sending failure during registration or resend no longer surfaces as an error, since the account was already created successfully — users can retry sending the verification email once mail delivery recovers.
- Dropped HTTPS redirection when nothing in the stack terminates TLS, removing a noisy warning on every request.

### Changed

- Backend API tests now mint their JWTs from the test server's actual configured secret instead of a hardcoded value, preventing spurious 401 failures when a developer's local config differs.
- Cache scope invalidation now uses a generation counter so permission/tenant revocation stays correct across instances once a shared cache is added.
- Production API Docker builds use the repo root as context so MinVer can resolve the release version from git tags.

## [v1.4.2] - 2026-08-09

### Added

- Single-instance Docker Compose setup for production (`docker-compose.prod.yml`): Dockerfiles for backend and frontend, an edge nginx serving the SPA and reverse-proxying `/api`/`/hubs` to the API (same-origin, `/hangfire` and `/metrics` deliberately unreachable through it), and a "Production (Docker)" section in the README.
- `backend/scripts/test.sh`: runs the 4 backend test projects as parallel processes after a single serialized build, cutting local suite wall-clock from ~56s to ~35s (479 tests). Documented in `commands.md` and README as the default way to run backend tests.

### Changed

- `.claude/rules/*.md` files now scope loading to matching `backend/`/`frontend/` paths instead of always loading into every session (`serena.md` stays always-loaded — it's tool-selection guidance needed before a session knows which files it'll touch).
- Removed `AGENTS.md`, which duplicated `CLAUDE.md`'s guidance but had drifted stale; `CLAUDE.md` is now the single source of truth.

## [v1.4.1] - 2026-08-05

### Changed

- Release process now goes through a `release/vX.Y.Z` QA-stabilization branch (`dev → release/vX.Y.Z → main`, two phases: cut and ship) instead of merging `dev` directly into `main`. Bugs found during stabilization branch off `release/vX.Y.Z` via `fix/*`. Neither the release branch nor the hotfix workflow back-merges into `dev` — unchanged from v1.2.0 for hotfix; if `dev` needs a hotfix's change too, cherry-pick or reimplement it separately.
- `lefthook.yml`'s `pre-commit` group now sets `piped: true`, so a failing command (e.g. `branch-guard` on `main`) stops the rest of the hooks instead of still running `secretlint`/`prettier` against a commit that's already going to be rejected.
- Pre-commit `prettier` and `bun run --cwd frontend format` now also cover `frontend/e2e/**`, matching `frontend/src/**`'s existing coverage — e2e specs were previously untouched by either.
- README (root and `frontend/`) updated to match the current feature set: multi-tenant Organizations + per-org RBAC, Notifications (in-app/email/push/SignalR), Hangfire/Prometheus/Grafana, and the corrected access model (no personal/org-less access to Files/ApiKeys/AuditLogs/SystemSettings). Previously described a pre-v1.2.0, single-account, no-roles model.

## [v1.4.0] - 2026-08-05

### Added

- MIT `LICENSE`, `CODE_OF_CONDUCT.md` (Contributor Covenant), and `SECURITY.md` (vulnerability reporting policy) for OSS readiness.

### Security

- Files, ApiKeys, AuditLogs, and SystemSettings are now scoped to the caller's active organization (resolved from the JWT `org_id` claim) — previously any authenticated account could read/modify another organization's records for these four resources.
- Added `CsrfProtectionMiddleware`, requiring a custom header on cookie-authenticated unsafe requests, so a forged cross-site `<form>` submission (e.g. to the file upload endpoint) can no longer ride along on the `access_token` cookie.

## [v1.3.0] - 2026-08-04

### Added

- Notification module Phase 2: `NotifyAsync` now fans out to background-dispatched channels via Hangfire (a dedicated `notifications` queue) instead of only persisting in-app — starting with an `EmailNotificationChannel` that reuses the existing SMTP sender. Per-channel failures are logged rather than retried, to avoid duplicate sends when Hangfire would otherwise retry the whole job. The organization-member-added notification now carries the organization's name, so both the in-app UI and the new email surface it instead of a generic placeholder.
- Notification module Phase 3: Web Push via Firebase Cloud Messaging, plugged into the same channel fan-out as email. Backend adds a `PushSubscription` entity and a `PushNotificationChannel` built on the official `FirebaseAdmin` SDK behind an `IPushSender` abstraction; invalid tokens are pruned reactively from the multicast send response. `FcmSettings` is optional — the channel simply doesn't register when unconfigured, so this doesn't affect existing setups. Frontend adds a push-notification toggle to the profile dialog, backed by a `usePushNotifications` composable and a static `firebase-messaging-sw.js` service worker; also optional via `VITE_FIREBASE_*` env vars.
- Notification module Phase 3B: real-time in-app delivery via SignalR, pushed synchronously in `NotifyAsync` rather than through the Hangfire fan-out (which would add poll-interval + DB round-trip latency). Backend adds an `IRealtimeNotifier` abstraction and a `NotificationHub`/`SignalRRealtimeNotifier` pair, running in-memory (no Redis backplane) for the current single-instance deployment. Frontend's notification store opens a hub connection alongside the existing 30s poll, which now acts purely as a fallback — paused while the hub is connected, resumed during SignalR's own reconnect attempts, and reconciled via a REST catch-up fetch once reconnected.

## [v1.2.0] - 2026-08-03

### Added

- Organizations (multi-tenant) support for the backend: each session scopes to at most one organization via a signed `org_id` JWT claim, a dedicated `POST /api/auth/switch-organization` endpoint, and `/api/organizations` for creating/listing organizations and managing members. Per-request tenant access is re-verified through a short-TTL in-memory cache so revocation takes effect quickly.
- Organizations UI: a sidebar switcher for the session's current organization (or personal workspace), and an `/organizations` page for creating organizations and managing members (add/remove/change role/deactivate).
- Custom RBAC: per-organization, configurable roles built from a fixed permission catalog, replacing the hardcoded Owner/Admin/Member enum. Effective permissions resolve per-request via a short-TTL cache and are never embedded in the JWT. Includes a role-management UI (create/edit/delete roles, assign multiple roles per member) and the active organization's permission set on the auth store.
- Correlation ID middleware (`X-Correlation-Id`, validated inbound or generated) plus structured per-request logging (`UseSerilogRequestLogging`) for the backend API.
- Repo-root Lefthook git hooks: pre-commit blocks direct commits to `main`, unresolved merge-conflict markers, and known secret formats (secretlint), and formats staged `frontend/src/` files with Prettier; commit-msg enforces Conventional Commits (commitlint).
- Hangfire (PostgreSQL storage) as reusable background-job infrastructure: server, dashboard (`/hangfire`), and DI wiring in `StarterKit.Infrastructure`, ready for fire-and-forget/delayed/recurring jobs with automatic retries in future work.
- Prometheus metrics endpoint (`/metrics`, via `prometheus-net.AspNetCore`) covering HTTP request rate/duration/errors and process/GC stats, plus a local docker-compose Prometheus + Grafana stack with a pre-provisioned datasource and "StarterKit API Overview" dashboard.

### Changed

- Split `StarterKit.Infrastructure`'s flat `Services/` folder into per-concern subfolders (Auth, Caching, Context, Email, Security, Storage), each with its own DI-registration `*Extensions` class.
- Replaced the custom `SvgIcon`/`vite-plugin-svg-icons` sprite system with `@vicons/tabler`.
- The `git-release` skill's hotfix workflow no longer back-merges `main` into `dev` after tagging.
- Consolidated `frontend/.gitignore` and `backend/.gitignore` into a single root `.gitignore`.
- Organization and role permission checks moved from inline service-layer checks to ASP.NET Core policy-based `[Authorize(Policy=...)]`, enforced in the authorization middleware before the action runs.
- Reduced the sidebar's minimal-mode width from `5rem` to `4rem`.
- All forms (login, register, resend-verification, profile, change-password, organization/role create-edit, add-member) now validate through naive-ui's `n-form`/`n-form-item`/`rules`/`FormInst` instead of hand-rolled `computed` error state; dialog-hosted forms block confirm on invalid input via a `validate()` exposed to `useAppDialogNaive`.
- `CLAUDE.md`/`serena.md` agent workflow guidance now routes caller-search and rename (Serena's `find_referencing_symbols`/`rename_symbol`) to backend C# only and to CodeGraph for any frontend symbol, based on empirical testing showing Serena silently misses callers inside `.vue` files regardless of import style.
- Refresh token cleanup moved from a `BackgroundService`/`PeriodicTimer` to a Hangfire recurring job; failures now retry automatically and surface in the Hangfire dashboard instead of being logged and swallowed.

### Fixed

- `MemoryCacheService.GetOrSetAsync` mistook a cache miss for a cached `false` when caching value types (e.g. `bool`) — an unconstrained generic `T?` erases to plain `T` for value types, so `default(T)` was indistinguishable from a real cached value, and the underlying factory was silently never invoked.
- OpenAPI declared enums (e.g. `OrganizationRole`) as `integer`, since the built-in generator has no visibility into the API's actual Newtonsoft `StringEnumConverter` formatter — generated clients typed them as `number` while the API only ever accepted the named strings on the wire. Added a schema transformer so enum schemas match runtime behavior.
- `POST /api/auth/switch-organization` had no way to return a session to its personal (org-less) context short of logging out, since token refresh always preserves the original `org_id`. `organizationId` is now nullable in the request.
- Frontend's `tests/` directory had never been type-checked (no script ran `vue-tsc -p tsconfig.vitest.json`), so it had accumulated ~85 type errors undetected. Fixed a generics bug in the shared `renderComponent` test helper that cascaded into most of them, plus stale sidebar icon fixtures, missing `node` types, and strict-null-check gaps; `tests/` now type-checks clean.
- Toast messages and confirm dialogs (Naive UI's discrete `message`/`dialog` API) always rendered with a light background, since the isolated Vue app instance they mount into was hardcoded to the light theme overrides regardless of the user's dark mode preference. Now stays in sync with the app's light/dark toggle.

### Removed

- GitNexus code-intelligence tooling (no longer in use): the `.claude/skills/gitnexus/` skill package and all `gitnexus_*` references in `CLAUDE.md`, `AGENTS.md`, `serena.md`, and the pre-task-reminder hook, replaced with CodeGraph (`codegraph_explore`) for macro-orientation and impact checks.
- `AccountsController` and the global admin CRUD-any-account surface: account creation goes through registration, profile edits are self-service only, and removal happens via organization membership, not the account itself.

## [v1.1.1] - 2026-07-25

### Fixed

- The release workflow's backend test step ran on a fresh checkout with no `appsettings.json` (gitignored); `StarterKit.API.Tests` boots the full web host and requires `CorsSettings`/`JwtSettings`/`EmailSettings` with no fallback, so every release run has failed since v1.0.0 before ever reaching the Create GitHub Release step. The workflow now seeds `appsettings.json` from `appsettings.Example.json` before running backend tests.

## [v1.1.0] - 2026-07-25

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
- Frontend app version (shown in the footer) is now derived from `git describe --tags --always` at build time instead of the static `package.json` version field, mirroring the backend's MinVer-derived versioning.
- The `git-release` skill's standard release workflow now runs the backend and frontend test suites directly on `dev` before finalizing the CHANGELOG or opening the release PR — a failing suite blocks the release.

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
