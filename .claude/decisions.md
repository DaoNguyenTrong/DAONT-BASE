# Decision Log

Record only **platform / cross-cutting** decisions that no single module owns — infrastructure, deployment, shared auth mechanics, build and test tooling, codegen, layer boundaries — and that affect system design: architecture, module/layer boundaries, API or data contracts, security posture, or other cross-cutting structural choices with a real trade-off, external constraint, or rejected alternative that would be costly to re-derive later. Out of scope: decisions owned by one module (they live in §12 of its `docs/tech/` document), working-process decisions (`docs/process/lifecycle.md`), visual/branding/UI styling, copy/content wording, and one-off bug fixes — even ones that took real investigation — unless the fix itself changed a structural/contract/security decision.
Newest entry at top — prepend new entries directly below this line, do not append at the bottom.
Title format: `### YYYY-MM-DD — <decision>` (date the decision was made, not a due date).
Write only the **why** — the reasoning and rejected alternative. Never "what was done" or "how" (git log/diff already has that; do not restate file names, method names, or a narrative of the change). Keep each entry under ~80 words (excluding heading).
**Language:** write every entry in English, even when the conversation that produced it is in another language.
**Who writes:** whoever makes the change, in the same PR as the change; `pr-reviewer` checks the entry. No separate approval.
**Append-only:** never edit or delete an existing entry. To reverse one, add a new entry with a `Supersedes: <old title>` line — such an entry needs the user's explicit approval.

---

### 2026-10-21 — Public files are served straight from storage; only `public/` is anonymously readable

Streaming avatars through the API puts every image view on the app, and presigned URLs have no Local equivalent and defeat browser caching. All providers instead serve files from `PublicUrlBase`; the S3 providers get an anonymous-read policy scoped to `public/*`, so org-scoped files stay API-only. Cost: anything under `public/` is readable by anyone holding the URL, and `PublicUrlBase` must be browser-reachable per environment.

### 2026-10-21 — Authorization fails closed: fallback policy plus an architecture test

Endpoints were public unless they carried `[Authorize]`, so a forgotten attribute silently exposed data. The fallback policy (the default policy: JWT or API key) makes it a 401 and also covers `Map...` endpoints; the architecture test names the offending action in CI. Cost: anonymous surfaces opt out explicitly — `/metrics` and `/hangfire` no longer skip auth implicitly (supersedes the 2026-08-03 monitoring note), and unmapped routes answer 401 to anonymous callers. Rejected: test only (misses non-controller endpoints), fallback only (silent until someone hits it).

### 2026-10-20 — Docs-only PRs merge through a gated project command

The repo has no reviewer account, GitHub does not let an author approve their own PR, and branch protection is unavailable on private repos under the Free plan. The merge gate therefore lives in the project's own `/git-accept` command: required CI checks green, agent review with no blocking finding, and docs-only scope. Rejected: manual-only merge (a bottleneck for a solo maintainer) and a GitHub App approver for now (needs a second identity). Code, workflows, rules and auth changes stay manual. The gate runs client-side, so it is not a hard control.

### 2026-10-17 — User guide is gated at the nginx edge against a session-only check, fail-closed

The guide stays a static site (keeps SSG and search, and in public mode survives app outages), so access control lives at the edge rather than in the SPA. The check accepts only the user-session scheme: the default policy also admits API keys, which would let a service credential read the guide. An API failure denies access instead of serving content. Rejected: guide as SPA routes; always public; fail-open on 5xx.

### 2026-10-17 — Guide content splits into base-owned and fork-owned trees

Forks must pull base guide updates without merge conflicts, so base and fork pages live in separate source trees merged into one URL space, with the table of contents composed from two files. Brand colours are generated from the app's Tailwind tokens rather than copied, so forks never edit them. Rejected: forks editing base pages directly (conflict on every upstream pull); duplicated colour values guarded by a drift test.

### 2026-10-16 — One-shot CLI commands run through the API host

Seed/import/admin tasks need the API's configuration and DI but not the web server, and must work when HTTP doesn't, from scripts branching on exit codes. Commands implement `ICliCommand` and run right after `Build()`, before any startup step. Rejected: EF or Infrastructure inside commands/`Program.cs` — migrations go through the `IDatabaseMigrator` Application port, so commands keep the Infrastructure-only-in-composition-root rule.

### 2026-10-16 — Each password check has its own lockout counter

The change-password current-password check had no limit, so a stolen session could brute-force it and then take the account over. It reuses the sign-in lockout on separate state. Rejected: sharing the sign-in counter — a session holder could then lock the owner out of signing in, and failed sign-ins would block the owner from changing a leaked password; a per-session rate limit alone — a fresh session resets it.

### 2026-10-16 — Login lockout reserves the attempt before the password check, in column-scoped SQL

Counting after BCrypt lets parallel guesses all read the same count, so a distributed attacker bypasses the limit; reserving first caps guesses per window at the cost of a burst locking the owner out sooner. Rejected: load-modify-save on the entity — a full-row write around the ~0.3s hash can revert a concurrent password change or deactivation. The lock is reported before verification; after it, the lock would act as a password oracle.

### 2026-10-16 — Namespaces must not form dependency cycles; mappers live per feature

A shared `Common/Mappings` mapper and ports in `Common/Interfaces` that name feature DTOs tied every feature namespace to every other, so no feature could change in isolation. Shared types now live with the side that is depended on, enforced by an architecture test rather than review. Rejected: a shrink-only allowlist (CIMP's interim step) — base had a single cycle, so the strict rule was cheaper than a ratchet.

### 2026-10-16 — Cache generation counters expire; Redis must run noeviction

Counters without a TTL leaked one key per removed cache key forever. A TTL is only safe if a counter outlives every entry it versions, so entry TTLs are capped (`MaxExpirationMinutes`, clamped rather than rejected — callers pass configured TTLs on every request) and every write extends the counters. Rejected: no TTL (unbounded growth). Consequence: any eviction policy, `volatile-*` included, can drop a counter early and resurrect stale entries, so `noeviction` is mandatory.

### 2026-08-25 — Cache + SignalR pick Memory/Redis via config; Redis is not hard-required

Single-instance stays in-memory; multi-instance turns on Redis via `CacheSettings:Provider` and `RealtimeSettings:Backplane`, sharing one `ConnectionStrings:Redis`. Rejected hard-wiring Redis into every environment — needless dependency/ops when running a single replica. Selecting Redis without a connection string fails fast, avoiding silent single-node behaviour when scaling out.

### 2026-08-25 — Cache scope invalidation via a generation counter, not key enumeration

`ICacheService.RemoveByPrefixAsync` was replaced by `InvalidateScopeAsync`, built on a per-scope generation counter instead of tracking/enumerating keys in-process. Local key tracking (the old approach) doesn't reach other instances once the cache is shared (Redis) — a permission revoke would stay stale on the other instances until the TTL ran out. Bumping a generation is atomic and correct for any shared backing store, so adding Redis needed no redesign. Also added `CacheSettings.Provider` + keyed DI to choose the provider via config, matching the `StorageExtensions` pattern.

### 2026-08-25 — backend/Dockerfile build context moved to the repo root so MinVer can see .git

`api.build.context` in `docker-compose.prod.yml` changed from `./backend` to `.` (`dockerfile: backend/Dockerfile`) — verified empirically that MinVer always falls back to `0.0.0-preview` when the context holds only `backend/`, because `.git` sits outside it, which made `/api/health.Version` useless for confirming what is deployed. The root `.dockerignore` and `backend/.dockerignore` were merged into one (`backend/.dockerignore` deleted) since both Dockerfiles now share the root context.

### 2026-08-25 — SeaweedFS storage provider: S3 Gateway API, bucket auto-created at startup

Added `SeaweedFsFileProvider` implementing the existing `IStorageProvider` (keyed DI on `StorageSettings:Provider`) — no change to `FileService`/`FilesController`/entities. Chose the S3 Gateway (AWSSDK.S3, `ForcePathStyle=true`) over the plain Filer HTTP API — heavier (extra dependency + an `-s3` container) but the code carries over to real AWS S3/MinIO later by changing only `ServiceURL`. A missing bucket is created at startup, favouring DX over requiring the operator to pre-create it. Local static-file serving (`Program.cs`) is now mounted only when `Provider == "Local"`; `FileDto.PublicUrl` only works with SeaweedFS once the bucket is configured public-read.

### 2026-08-09 — Backend test runner parallelised across OS processes instead of dropping -m:1

`dotnet test backend/StarterKit.sln --no-restore -m:1` serialises all 4 test projects, though most of the wall-clock (55.8s for 479 tests) is per-project build/process-startup overhead, not test execution — verified empirically. Kept `-m:1` (dropping it risks re-triggering the known parallel-MSBuild bug). Instead `backend/scripts/test.sh` builds once, serially, then runs each project as an independent `dotnet test --no-build` process (still `-m:1` internally) — down to ~35s, same test results.

### 2026-08-07 — Docker production: one edge nginx (same-origin), appsettings.json bind-mounted instead of baked into the image

Chose a single nginx that both serves the static frontend and proxies `/api`, `/hubs` to the API, instead of a separate reverse-proxy container — every request becomes same-origin, so the real domain needn't be known at image build time (`VITE_API_BASE_URL` left empty). Backend secrets keep the existing convention (`appsettings.Example.json` → gitignored `appsettings.json`) but are bind-mounted into the container rather than baked into the image or spread over dozens of nested env vars. Single-instance only — cache and SignalR are in-memory; don't scale `api` beyond 1 replica until Redis is added.

### 2026-08-04 — Notification email content defaults to `vi`, not per-account locale

`Account` has no locale column, and a Hangfire worker dispatching email has no HTTP request context to infer the user's current language preference — unlike the frontend, which always knows the active locale. Rather than block Phase 2 on adding `Account.Locale`, email content (`NotificationEmailTemplates`) is `vi`-only system-wide, matching `localization.md`'s stated default. Per-account locale is deferred until a real need surfaces (e.g. an `en`-preferring user complaining).

### 2026-08-04 — Notification dispatch: log-and-continue per channel, no Hangfire retry

`NotificationDispatcher` catches each `INotificationChannel` failure and logs rather than rethrowing. Rejected alternative: let a channel's exception propagate and let Hangfire retry the whole job — this would re-send every channel that already succeeded (duplicate emails) whenever one channel fails. Trade-off accepted: a failing channel is not retried by Hangfire on its own; acceptable with only one channel (Email) in Phase 2.

### 2026-08-04 — Background job queue selection via `EnqueuedState`, not Hangfire's `[Queue]` attribute

`IBackgroundJobDispatcher.Enqueue<TJob>` takes an explicit `queueName` string parameter (deviating from the original class diagram's no-argument signature) so `HangfireJobDispatcher` can route jobs to the `notifications` queue via `IBackgroundJobClient.Create(job, new EnqueuedState(queueName))`. Rejected alternative: Hangfire's `[Queue("notifications")]` attribute on the dispatched method — would have forced `Application` to reference the Hangfire package directly, breaking the layer boundary that keeps Application framework-agnostic.

### 2026-08-03 — Prometheus/Grafana monitoring: prometheus-net over OpenTelemetry, local-dev scope only

Chose prometheus-net.AspNetCore over OpenTelemetry .NET — OTel's official Prometheus exporter (`OpenTelemetry.Exporter.Prometheus.AspNetCore`) is still beta (1.17.0-beta.1) at this point, not fit to ship in a starter kit reused across projects. `/metrics` sits outside `/api`, so like `/hangfire` it skips `UserTimeZoneMiddleware`/rate limiter/auth automatically — keeping the "open, restricted at the network layer" model instead of adding auth code. Scope this time is local dev only (docker-compose scrapes via host.docker.internal) — the API isn't containerised for production yet.

### 2026-08-03 — Add Hangfire (PostgreSQL storage) as reusable background-job infrastructure

Wired ahead of any concrete job need, since this repo is a starter kit reused across future projects — one-time setup beats redoing it per project. Rejected keeping the existing `BackgroundService`/`PeriodicTimer` pattern as the general answer: it doesn't survive restarts and would double-run under multiple instances, which Hangfire's storage-backed scheduling avoids for free (same Postgres DB, no new infra). Dashboard (`/hangfire`) has no app-level identity check — access is restricted at the network layer (internal LAN only, not internet-exposed) per explicit instruction, so Hangfire's default loopback-only filter was replaced with none rather than left in (it would wrongly block real LAN clients once `UseForwardedHeaders()` rewrites the client IP). Migrated `RefreshTokenCleanupService` to a Hangfire recurring job as the reference example; this also changes its failure handling from log-and-swallow to Hangfire's own retry-and-surface-in-dashboard model.

### 2026-08-02 — Removed `AccountsController`: account management moved entirely into organization scope

Dropped the global CRUD-any-account surface (no authorization beyond "logged in") — every capability
already has a replacement: creation via `/api/auth/register`, profile edits are self-service only via
`ProfileController` (no admin-edits-others-profile), and removal/deactivation happens by removing/
deactivating an `OrganizationMember` (`RemoveMemberAsync`), not deleting or locking the `Account`
itself. Eliminates the "every authenticated account has equal access to every other account" model,
making enforcement consistent with the permission-based RBAC now applied to Organizations.

### 2026-08-02 — Authorization: organization/role permission checks moved to ASP.NET Core policy-based `[Authorize]`

`EnsurePermissionAsync`/`EnsureActiveMemberAsync` moved out of `OrganizationService`/`RoleService` into a custom
`IAuthorizationRequirement`+`IAuthorizationHandler`+`IAuthorizationPolicyProvider`, triggered via
`[Authorize(Policy=...)]` on controller actions — enforcement now runs in ASP.NET Core's authorization
middleware before the action executes, not inline in application code. No change to what's checked, the
cache, or JWT contents — only where the check happens. Also required a custom
`IAuthorizationMiddlewareResultHandler`: the framework's default handler writes an empty 403 body, which
would have silently dropped the existing localized `CodedProblemDetails` error shown in the frontend.

### 2026-08-02 — Authorization: switch to RBAC with per-organization custom roles

Replaces the hardcoded `OrganizationRole` enum (Owner/Admin/Member) with configurable Role/Permission,
each org authoring its own roles — chosen over a global role catalog or a seed-only hybrid to maximize
per-tenant flexibility, accepting the heavier cost (Role CRUD API+UI required from v1). Permissions stay
a fixed code-defined catalog (not user-creatable); only Roles (permission bundles) are configurable.
Effective permissions resolve per-request via a short-TTL cache + invalidation (same pattern as the
existing `org_id`/`TenantAccessService` decision), not embedded in the JWT, to keep revocation near-instant.

### 2026-08-02 — OpenAPI enum schemas rewritten to strings via a schema transformer, not per-enum attributes

The built-in OpenAPI generator is System.Text.Json-based and has no visibility into the app's
actual formatter (Newtonsoft, with a global `StringEnumConverter`), so it declared every enum as
`type: integer` — a real contract mismatch, since the API rejects integers on the wire. Fixed with
a generic `EnumSchemaTransformer` that rewrites any enum schema to named strings, rather than a
`[JsonConverter(JsonStringEnumConverter)]` attribute per enum — the attribute approach would need
repeating for every enum added later and is easy to forget.

### 2026-08-02 — Switch-organization endpoint accepts a null organizationId to return to "Personal"

`SwitchOrganizationAsync`/`SwitchOrganizationRequest.OrganizationId` became nullable so a session
can switch back to the account's personal (org-less) context, not just between organizations.
Without this, the only way to leave an org-scoped session was to log out and back in, since
`RefreshTokenAsync` always preserves whatever `org_id` the original token carried. A null target
skips the membership check entirely — there's nothing to authorize, every account is implicitly
authorized for its own personal context.

### 2026-08-02 — Organizations: JWT-embedded tenant claim, not a client-supplied header

Each session is scoped to at most one organization, carried as a signed `org_id` claim on the
access token rather than a client-supplied header — a header would let any caller assert tenant
identity without re-authentication. Switching orgs re-issues tokens via a dedicated endpoint
instead of requiring logout/login. Per-request access is still re-verified (org active, membership
active) via a short-TTL in-memory cache, so revocation takes effect within seconds without a DB hit
per request; acceptable only because the app runs single-instance today — a multi-instance
deployment would need a distributed cache instead.

### 2026-07-29 — Serilog sinks are fully config-driven; no code-level Console fallback

`Program.cs` hardcoded `.WriteTo.Console()` alongside `ReadFrom.Configuration(...)`, duplicating
every console log line since `appsettings.Example.json` already configures a Console+File `WriteTo`
block (confirmed against the local `appsettings.json`). Removed the hardcoded sink rather than the
config block. Trade-off: a developer who strips the `Serilog` config section entirely now gets zero
console output instead of a fallback — acceptable since the Example file ships the block by default.

### 2026-07-26 — Infrastructure/Services split into per-concern modules; avoided naming one "System"

Flat Services/ folder mixed auth, email, storage, caching, security, and request-context concerns
with no grouping. Split into per-concern subfolders, namespace mirroring folder path (matches the
existing Persistence/Repositories convention); DI-registration `*Extensions` classes keep the root
`StarterKit.Infrastructure` namespace so `DependencyInjection.cs` needs only one `using`. Rejected
naming the clock/timezone module "System" — it shadowed the BCL `System` namespace for sibling
files, breaking unqualified `System.*` references; renamed to `Context`. Also moved cache/timezone/
current-user/secret-protector/cleanup DI registrations out of `PersistenceExtensions` into their
owning module.

### 2026-07-25 — Microsoft external login: multi-tenant issuer validated by regex, no email_verified check

`common` tenant's OIDC discovery document reports `issuer` as a literal `{tenantid}` placeholder
(no single fixed issuer exists across tenants), so `ValidIssuer` couldn't be set directly — used a
custom `IssuerValidator` that turns the placeholder into a regex instead. Also skipped Google's
`email_verified` check: Microsoft ID tokens carry no such claim on either work/school or personal
accounts, since Microsoft itself guarantees the email at the tenant/MSA level. Rejected an extra
Microsoft Graph `/me` call to double-check — adds a network round-trip and a `User.Read` scope for
a guarantee the token issuer already provides.

### 2026-07-25 — Social login buttons: divider extracted out of GoogleLoginButton into a shared component

`GoogleLoginButton.vue` used to render its own "Or continue with" divider inline. Adding
`MicrosoftLoginButton.vue` alongside it would have stacked two dividers. Extracted the divider into
`SocialLoginDivider.vue` (shown once if any provider's client ID is configured), used by both
`LoginView.vue` and `RegisterView.vue` ahead of the provider buttons — kept each provider button
component only responsible for its own button/error/resend state.

### 2026-07-25 — Refresh-token flow: interceptor delegates to the store instead of duplicating the call

`client.ts`'s 401-retry interceptor had its own inline `refreshClient.post('/api/auth/refresh', {})`
+ `setAuth(...)`, duplicating `stores/auth.ts`'s `refreshToken` action almost verbatim (pre-existing,
not introduced by the codegen migration — just carried over). Changed the interceptor to call
`auth.refreshToken()` directly — same `refreshClient` instance either way (no `isRefreshing`/
`failedQueue` interaction inside the action, so no behavior change), one implementation instead of
two that could silently drift. Also found the interceptor's refresh-failure path cleared auth but
not the `keep-login` localStorage flag (unlike `logout()`/`restoreSession`'s failure path) — harmless
(next boot just retries and fails once more) but inconsistent; added the same `clearKeepLoginPreference()`
call there. Covered both by a new test in `client.test.ts`.

### 2026-07-25 — Removed the `{resource}-api.ts` wrapper layer; orchestration moved into Pinia stores

Per explicit user request, deleted `account-api.ts`/`auth-api.ts`/`health-api.ts`/`profile-api.ts`
— call sites now import generated clients directly. The side effects those wrappers used to own
(`setAuth()` after login/register/verifyEmail/externalLogin, the `refreshClient`-not-`apiClient`
special case for token refresh, session-id `number|string` normalization) had to move somewhere:
put auth orchestration into `stores/auth.ts` as actions (idiomatic Pinia, keeps it centralized
instead of scattered `useAuthStore().setAuth(...)` calls across 5+ views) rather than inlining at
every call site. Profile/health/accounts have no such side effects, so those call the generated
`get*()` factories directly with no intermediate layer. `PagedResult<T>` normalization (still
needed — see below) extracted to a pure `lib/paged-result.ts` helper, not a per-resource wrapper.

### 2026-07-25 — OpenAPI codegen: orval + generated-but-gitignored, mirrors src/typings/

Frontend uses `orval` (full client, per user choice over types-only `openapi-typescript`) generating
into `frontend/src/api/generated/**`, gitignored and regenerated via a `postinstall` script — same
convention as the existing gitignored `src/typings/` (auto-import .d.ts files), avoids committed-
generated-code drifting from `shared/openapi/openapi.json`. Hand-written `{resource}-api.ts` stay as
a thin layer over generated calls (side effects like `setAuth()` after login aren't spec-expressible).
`authApi.refreshToken` stays fully hand-written — must use the separate `refreshClient` (no 401-retry
interceptor) to avoid recursing into `apiClient`'s own refresh logic; routing it through the shared
mutator would have silently broken that isolation.

### 2026-07-25 — Backend OpenAPI doc generation: off by default on `dotnet build`

`Microsoft.Extensions.ApiDescription.Server`'s `OpenApiGenerateDocumentsOnBuild` defaults to `true`
(generates on every build), costing ~5-9s via a design-time host on every single `dotnet build` —
unacceptable given `dotnet build backend/StarterKit.sln` is the standard command used everywhere
(commands.md, CLAUDE.md's Verify step). Set to `false`; regeneration is an explicit opt-in command
(`-p:OpenApiGenerateDocumentsOnBuild=true`), same spirit as EF migrations being explicit, not automatic.

### 2026-07-25 — Controller routes normalized to lowercase (not a codegen workaround)

4 of 8 controllers used `[Route("api/[controller]")]`, yielding PascalCase paths (`/api/Auth/login`)
in the OpenAPI spec — ASP.NET Core routing is case-insensitive so this never broke real traffic, but
it broke MSW test mocks and orval-generated client paths once codegen started reading the spec
literally, and was inconsistent with the other 4 controllers' explicit lowercase routes. Fixed by
declaring explicit lowercase routes on all 8 (root-cause fix, not a per-consumer workaround) — added
`QueryParameterCasingTransformer` for the same reason on DTO-bound query params (PageNumber etc.).

### 2026-07-25 — Generated int64 fields (`number | string`) normalized at the api-layer boundary, not propagated

.NET's OpenAPI generator widens `long`/`int` response fields to a `number | string` union (JS-safe-
integer interop) — e.g. `PagedResultOf*Dto` fields, `SessionDto.id`. Rather than let that union type
leak into views/stores (pagination arithmetic, session-id comparisons), `account-api.ts`/`auth-api.ts`
coerce to plain `number` before returning, keeping `PagedResult<T>`/`SessionDto` as stable hand-written
frontend-only shapes. Everything else in `types.ts` is now a direct re-export of the generated model
type under its existing name — kept the migration transparent to every existing call site.

### 2026-07-25 — Rate limiting behind a reverse proxy: trust forwarded headers only from configured proxies

Rate limiting keys on `Connection.RemoteIpAddress`, which is the proxy's own IP for every request
once the app sits behind any reverse proxy/LB — collapsing all real clients into one rate-limit
bucket (and breaking IP-based audit/abuse tracking). Added `UseForwardedHeaders()` bound from a
new `ForwardedHeadersSettings:KnownProxies`/`KnownNetworks` config (both default empty — ASP.NET
Core's own default of loopback-only trust applies, so forwarded headers are silently ignored,
i.e. no-op, until an operator explicitly lists their actual proxy/LB IP or CIDR). Rejected
clearing `KnownProxies`/`KnownNetworks` to trust any forwarder unconditionally — that lets any
client spoof `X-Forwarded-For` to bypass the rate limiter entirely if the app is ever reachable
directly (no proxy in front, e.g. misconfigured deployment or direct port exposure). This is a
starter kit with no fixed deployment topology, so the trusted-proxy set has to be a per-deployment
config knob, not a hardcoded assumption.

### 2026-07-25 — CORS: explicit allowlist instead of reflect-any-origin

`Program.cs` used `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` — reflects any Origin
back with credentials enabled, which defeats the browser's same-origin protections entirely
(any site can call the API using the victim's cookies). Replaced with `WithOrigins()` bound from
a new `CorsSettings:AllowedOrigins` config array (same required-config pattern as `JwtSettings` —
throws at startup if missing/empty, rather than silently falling back to permissive). Default
dev value is `http://localhost:5173` (frontend's Vite port).
