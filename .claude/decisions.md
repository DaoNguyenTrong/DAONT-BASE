# Decision Log

Record only decisions that affect **system design** — architecture, module/layer boundaries, API or data contracts, security posture, or other cross-cutting structural choices with a real trade-off, external constraint, or rejected alternative that would be costly to re-derive later. Out of scope: visual/branding/UI styling, copy/content wording, and one-off bug fixes — even ones that took real investigation — unless the fix itself changed a structural/contract/security decision.
Newest entry at top — prepend new entries directly below this line, do not append at the bottom.
Title format: `### YYYY-MM-DD — <decision>` (date the decision was made, not a due date).
Write only the **why** — the reasoning and rejected alternative. Never "what was done" or "how" (git log/diff already has that; do not restate file names, method names, or a narrative of the change). Keep each entry under ~80 words (excluding heading).
**Gate:** never write to this file without asking first — draft the proposed entry, show it to the user, and wait for explicit confirmation before prepending it.

---

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
