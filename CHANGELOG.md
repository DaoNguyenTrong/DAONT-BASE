# Changelog

All notable changes to this project will be documented in this file.

## [Unreleased]

## [v1.5.2] - 2026-09-03

### Changed

- The release workflow runs backend tests via `backend/scripts/test.sh`, matching the canonical local test command instead of duplicating a raw `dotnet test` invocation.

## [v1.5.1] - 2026-09-03

### Changed

- Release and hotfix workflows now run from the shared `release-kit` plugin (`/git-release`) instead of a project-local skill copy, with bindings in `.claude/release-kit.json`. After a release or hotfix ships, `main` is now reconciled back into `dev` rather than left un-merged.

## [v1.5.0] - 2026-09-01

### Added

- Optional Redis for shared app cache and SignalR so multiple API instances stay consistent when scaled out.
- Optional SeaweedFS file storage via its S3 gateway, with a local docker-compose service for development.
- The cache provider is selectable via config, matching the existing storage-provider pattern.
- A full-screen error screen while the backend is unreachable; the app recovers on its own once the API returns.
- After signing in, you return to the page you originally requested instead of always the home screen.

### Changed

- Upgraded local and production Postgres to 18.
- Removed the bundled Redis container from docker-compose; a scaled-out deployment supplies its own Redis.
- Switched the production API Docker build to the repo-root context so MinVer can resolve the release version from git tags.
- Reworked cache scope invalidation onto a generation counter so permission and tenant revocation stay correct once a shared multi-instance cache is enabled.
- The local development Postgres now listens on port 5433 to avoid clashing with a Postgres already running on the host default 5432.

### Fixed

- Email verification links now work with the app's hash-based routing — the link previously bounced unauthenticated users to the login page instead of the verification screen.
- A temporary email failure during registration or resend no longer surfaces as an error — the account is already created, and you can resend the verification email once mail delivery recovers.
- The API no longer logs an HTTPS-redirection warning on every request when nothing in the stack terminates TLS.
- Opening the app in a second tab no longer risks logging you out of the first while both refresh their session.
- The organization switcher menu no longer detaches to the sidebar corner; it now anchors to its trigger button.

### Security

- A stolen refresh token replayed after its session has already moved on now invalidates the entire session family.

## [v1.4.2] - 2026-08-09

### Added

- Single-instance production Docker Compose setup (`docker-compose.prod.yml`): backend and frontend images plus an edge nginx that serves the SPA and proxies `/api` and `/hubs` same-origin, with `/hangfire` and `/metrics` deliberately unreachable. Documented in the README.
- `backend/scripts/test.sh` runs the backend test projects in parallel after one serialized build, cutting the local suite from ~56s to ~35s. Now the documented default for running backend tests.

### Changed

- Agent rule files (`.claude/rules/*.md`) now load only for sessions touching the matching `backend/` or `frontend/` paths instead of always; `serena.md` stays always-loaded as tool-selection guidance.
- Removed the stale `AGENTS.md`; `CLAUDE.md` is now the single source of agent guidance.

## [v1.4.1] - 2026-08-05

### Changed

- Release process now goes through a `release/vX.Y.Z` QA-stabilization branch (`dev → release/vX.Y.Z → main`) instead of merging `dev` straight into `main`. Fixes during stabilization branch off via `fix/*`; the release branch is not back-merged into `dev`.
- The pre-commit hook now stops at the first failing check instead of running the rest against a commit that will be rejected anyway.
- Pre-commit formatting now also covers `frontend/e2e/**`, not just `frontend/src/**`.
- README (root and `frontend/`) rewritten to match the current feature set — multi-tenant Organizations with per-org RBAC, Notifications, and observability — replacing the outdated pre-v1.2.0 description.

## [v1.4.0] - 2026-08-05

### Added

- MIT `LICENSE`, `CODE_OF_CONDUCT.md` (Contributor Covenant), and `SECURITY.md` (vulnerability reporting policy) for OSS readiness.

### Security

- Files, API keys, audit logs, and system settings are now scoped to the caller's active organization — previously any authenticated account could read or modify another organization's records for these resources.
- Cookie-authenticated unsafe requests now require a custom header, so a forged cross-site form submission can no longer ride along on the `access_token` cookie.

## [v1.3.0] - 2026-08-04

### Added

- Notifications now fan out to background-dispatched channels via Hangfire instead of only persisting in-app, starting with email over the existing SMTP sender. The organization-member-added notification carries the organization name so both the email and the in-app UI show it.
- Web Push notifications via Firebase Cloud Messaging, on the same channel fan-out as email, with a push toggle in the profile dialog. Optional — the channel stays unregistered until `FcmSettings`/`VITE_FIREBASE_*` are configured, so existing setups are unaffected.
- Real-time in-app notification delivery via SignalR, pushed immediately rather than through the Hangfire fan-out. The 30-second poll is now a fallback — paused while the hub is connected and reconciled with a catch-up fetch on reconnect. Runs in-memory (no Redis backplane) for the single-instance deployment.

## [v1.2.0] - 2026-08-03

### Added

- Multi-tenant Organizations for the backend: each session scopes to at most one organization via a signed `org_id` JWT claim, with endpoints to switch organization and to create, list, and manage members. Tenant access is re-verified per request through a short-TTL cache so revocation takes effect quickly.
- Organizations UI: a sidebar switcher for the current organization (or personal workspace) and an `/organizations` page for creating organizations and managing members.
- Custom per-organization RBAC: configurable roles built from a fixed permission catalog, replacing the hardcoded Owner/Admin/Member enum. Permissions resolve per request via a short-TTL cache and are never embedded in the JWT. Includes a role-management UI.
- Correlation-ID middleware (`X-Correlation-Id`, validated or generated) and structured per-request logging for the backend API.
- Repo-root Lefthook git hooks: pre-commit blocks direct commits to `main`, merge-conflict markers, and known secret formats, and formats staged frontend files; commit-msg enforces Conventional Commits.
- Hangfire (PostgreSQL storage) as reusable background-job infrastructure — server, dashboard, and DI wiring — ready for fire-and-forget, delayed, and recurring jobs.
- Prometheus metrics endpoint (`/metrics`) covering HTTP and process/GC stats, plus a local Prometheus + Grafana docker-compose stack with a pre-provisioned dashboard.

### Changed

- Split `StarterKit.Infrastructure`'s flat `Services/` folder into per-concern subfolders, each with its own DI-registration class.
- Replaced the custom `SvgIcon`/`vite-plugin-svg-icons` sprite system with `@vicons/tabler`.
- The `git-release` skill's hotfix workflow no longer back-merges `main` into `dev` after tagging.
- Consolidated `frontend/.gitignore` and `backend/.gitignore` into a single root `.gitignore`.
- Organization and role permission checks moved from inline service-layer checks to policy-based `[Authorize]`, enforced before the action runs.
- Reduced the sidebar's minimal-mode width from `5rem` to `4rem`.
- All forms now validate through naive-ui's `n-form`/`rules` instead of hand-rolled error state; dialog-hosted forms block confirm on invalid input.
- Agent workflow guidance now routes Serena's caller-search and rename to backend C# only, using CodeGraph for any frontend symbol — Serena silently misses callers inside `.vue` files.
- Refresh-token cleanup moved from a `BackgroundService` timer to a Hangfire recurring job; failures now retry and surface in the dashboard instead of being logged and swallowed.

### Fixed

- `MemoryCacheService.GetOrSetAsync` mistook a cache miss for a cached `false` when caching value types, so the factory was silently never invoked.
- OpenAPI declared enums as integers even though the API only accepts the named strings on the wire, so generated clients typed them wrong. A schema transformer now matches runtime behavior.
- `switch-organization` had no way to return a session to its personal (org-less) context short of logging out; the request's `organizationId` is now nullable.
- The frontend `tests/` directory was never type-checked and had accumulated ~85 undetected type errors — a generics bug in the shared `renderComponent` helper plus stale fixtures and null-check gaps. It now type-checks clean.
- Toast messages and confirm dialogs always rendered light because their isolated Vue app instance ignored the dark-mode preference; they now follow the app's light/dark toggle.

### Removed

- GitNexus code-intelligence tooling, replaced by CodeGraph for macro-orientation and impact checks.
- `AccountsController` and the global admin CRUD-any-account surface: account creation goes through registration, profile edits are self-service, and removal happens via organization membership.

## [v1.1.1] - 2026-07-25

### Fixed

- Every release run had failed since v1.0.0: the backend test step ran on a fresh checkout with no `appsettings.json`, which `StarterKit.API.Tests` requires with no fallback. The workflow now seeds it from `appsettings.Example.json` first.

## [v1.1.0] - 2026-07-25

### Security

- CORS previously reflected any Origin with credentials enabled (worse than a wildcard); it now requires an explicit `CorsSettings:AllowedOrigins` allowlist.
- Rate limiting behind a reverse proxy collapsed every client into one bucket, since it keyed on the socket IP with no forwarded-headers handling. Added opt-in `ForwardedHeadersSettings`, defaulting to loopback-only trust so it's a no-op until an operator configures their proxy.

### Added

- Microsoft sign-in (MSAL.js popup + PKCE) alongside the existing Google option, on the backend's provider-agnostic external-login flow.
- The backend exports its OpenAPI spec to `shared/openapi/openapi.json` at build time (off by default), with stable OperationIds and camelCase query parameters.
- The frontend generates its API client from that spec via `orval` (also on `bun install`) — the backend/frontend contract is no longer hand-synced.
- `RENAMING.md` — a checklist for renaming the project, with the pitfalls to avoid (DB name, JWT issuer/audiences).
- Weatherplus branding: a logo-derived color palette, wordmark/mark assets across the sidebar and auth pages, and a regenerated favicon/PWA icon set.

### Changed

- Restyled the Google sign-in button to a full-width text+logo button matching Microsoft's, with a shared "Or continue with" divider.
- Normalized 4 controller routes to explicit lowercase — ASP.NET routing is case-insensitive, but the OpenAPI spec captures the declared casing and codegen reproduces it, so this keeps generated paths consistent with existing callers.
- Removed the hand-written `{account,auth,health,profile}-api.ts` wrapper modules; callers use the generated client directly, with spec-inexpressible side effects moved into `stores/auth.ts`.
- Deduplicated the refresh-token call between the axios 401-retry interceptor and the auth store — the interceptor now delegates to `authStore.refreshToken()`.
- Dark mode retuned to an explicit navy base (`#121527`/`#1e2235`) with retinted surface tokens and a more saturated primary violet.
- The frontend footer version now comes from `git describe` at build time instead of the static `package.json` field, mirroring the backend's MinVer versioning.
- The `git-release` standard workflow now runs both test suites on `dev` before finalizing the CHANGELOG or opening the release PR — a failing suite blocks the release.

### Fixed

- The 401 refresh-failure path now also clears the "keep me logged in" preference, matching `logout()` — previously it only cleared on the next app boot.
- `.claude/rules/authentication.md` described a multi-tenancy system that doesn't exist in the codebase; corrected to match actual auth behavior.

## [v1.0.0] - 2026-07-24

### Security

- Upgraded Microsoft packages 10.0.8 → 10.0.10 to resolve five high-severity DoS advisories on transitive `System.Security.Cryptography.Xml`.

### Added

- Full backend test coverage across all layers — every Application service, a new `Infrastructure.Tests` project (services plus Testcontainers-backed repository tests), and a new `API.Tests` project (`WebApplicationFactory` + Testcontainers integration tests across all controllers and middleware).
- `RateLimiterSettings` config seam for `AuthController`'s rate limiter (production default unchanged).
- Additional backend test coverage from a follow-up review: JWT rejection, oversized upload, validation-400 paths, auth cookie flags, and expanded domain-entity cases.
- Frontend test coverage from a test-gap audit: `AccountsView.vue`, concurrent-401 refresh queueing, and the router's auth guard.
- `@vitest/coverage-v8` wired up for the frontend (`bun run test:coverage`) to measure real line/branch coverage instead of file-presence heuristics.
- Frontend test coverage for the app-shell layout components, previously untested despite rendering on every authenticated page.

### Fixed

- A non-deterministic `CreatedAt`-ordering repository test (audit stamping overwrote seeded timestamps); fixed to seed a genuine difference.
- An email-collision test used an over-permissive mock that never exercised the self-exclusion check; tightened.
- Admin account creation (`POST /api/accounts`) returned a generic 500 on a duplicate username/email instead of 409 Conflict; it now pre-checks uniqueness like registration.

### Changed

- `GoogleAuthProvider` and `SmtpEmailSender` refactored behind thin interfaces for unit testing — no behavior change.
- The frontend router's `beforeEach` guard extracted into an exported `resolveGuardRedirect` for unit testing — no behavior change.
- README translated to English.
- `.claude/decisions.md` convention tightened to record only the *why*, with a lower per-entry word cap; prior entries cleared.

### StarterKit baseline

This repo was repurposed from a product-specific codebase into a generic app starter. Baseline state:

- Multi-tenancy (Tenant/TenantMembership/TenantRole, `X-Tenant-Id`) removed — single-user/single-account model.
- Kept: email+password auth with mandatory email verification, Google social login, session management, Account/Profile, ApiKey, AuditLog, File storage, SystemSettings.
- EF Core migrations squashed to a single `InitialCreate` migration.
- Project renamed (namespaces, solution, packages) to `StarterKit`.
- Added `docker-compose.yml` (Postgres + Mailpit) for local dev bootstrap.

See `.claude/decisions.md` for the architectural decisions still relevant to this baseline (auth/session design, error envelope shape, localization approach, etc.).
