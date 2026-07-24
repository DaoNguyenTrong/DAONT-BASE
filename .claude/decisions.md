# Decision Log

Expensive, non-obvious architectural decisions for StarterKit — real trade-offs, external constraints, rejected alternatives, or anything a future session could easily get wrong without the reasoning. Skip routine decisions (standard CRUD, straightforward bug fixes, following an existing pattern) — they add noise without future value.
Newest entry at top — prepend new entries directly below this line, do not append at the bottom.
Write only the **why** — the reasoning and rejected alternative. Never "what was done" or "how" (git log/diff already has that; do not restate file names, method names, or a narrative of the change). Keep each entry under ~80 words (excluding heading).

---

### Refresh-token flow: interceptor delegates to the store instead of duplicating the call

`client.ts`'s 401-retry interceptor had its own inline `refreshClient.post('/api/auth/refresh', {})`
+ `setAuth(...)`, duplicating `stores/auth.ts`'s `refreshToken` action almost verbatim (pre-existing,
not introduced by the codegen migration — just carried over). Changed the interceptor to call
`auth.refreshToken()` directly — same `refreshClient` instance either way (no `isRefreshing`/
`failedQueue` interaction inside the action, so no behavior change), one implementation instead of
two that could silently drift. Also found the interceptor's refresh-failure path cleared auth but
not the `keep-login` localStorage flag (unlike `logout()`/`restoreSession`'s failure path) — harmless
(next boot just retries and fails once more) but inconsistent; added the same `clearKeepLoginPreference()`
call there. Covered both by a new test in `client.test.ts`.

### Removed the `{resource}-api.ts` wrapper layer; orchestration moved into Pinia stores

Per explicit user request, deleted `account-api.ts`/`auth-api.ts`/`health-api.ts`/`profile-api.ts`
— call sites now import generated clients directly. The side effects those wrappers used to own
(`setAuth()` after login/register/verifyEmail/externalLogin, the `refreshClient`-not-`apiClient`
special case for token refresh, session-id `number|string` normalization) had to move somewhere:
put auth orchestration into `stores/auth.ts` as actions (idiomatic Pinia, keeps it centralized
instead of scattered `useAuthStore().setAuth(...)` calls across 5+ views) rather than inlining at
every call site. Profile/health/accounts have no such side effects, so those call the generated
`get*()` factories directly with no intermediate layer. `PagedResult<T>` normalization (still
needed — see below) extracted to a pure `lib/paged-result.ts` helper, not a per-resource wrapper.

### OpenAPI codegen: orval + generated-but-gitignored, mirrors src/typings/

Frontend uses `orval` (full client, per user choice over types-only `openapi-typescript`) generating
into `frontend/src/api/generated/**`, gitignored and regenerated via a `postinstall` script — same
convention as the existing gitignored `src/typings/` (auto-import .d.ts files), avoids committed-
generated-code drifting from `shared/openapi/openapi.json`. Hand-written `{resource}-api.ts` stay as
a thin layer over generated calls (side effects like `setAuth()` after login aren't spec-expressible).
`authApi.refreshToken` stays fully hand-written — must use the separate `refreshClient` (no 401-retry
interceptor) to avoid recursing into `apiClient`'s own refresh logic; routing it through the shared
mutator would have silently broken that isolation.

### Backend OpenAPI doc generation: off by default on `dotnet build`

`Microsoft.Extensions.ApiDescription.Server`'s `OpenApiGenerateDocumentsOnBuild` defaults to `true`
(generates on every build), costing ~5-9s via a design-time host on every single `dotnet build` —
unacceptable given `dotnet build backend/StarterKit.sln` is the standard command used everywhere
(commands.md, CLAUDE.md's Verify step). Set to `false`; regeneration is an explicit opt-in command
(`-p:OpenApiGenerateDocumentsOnBuild=true`), same spirit as EF migrations being explicit, not automatic.

### Controller routes normalized to lowercase (not a codegen workaround)

4 of 8 controllers used `[Route("api/[controller]")]`, yielding PascalCase paths (`/api/Auth/login`)
in the OpenAPI spec — ASP.NET Core routing is case-insensitive so this never broke real traffic, but
it broke MSW test mocks and orval-generated client paths once codegen started reading the spec
literally, and was inconsistent with the other 4 controllers' explicit lowercase routes. Fixed by
declaring explicit lowercase routes on all 8 (root-cause fix, not a per-consumer workaround) — added
`QueryParameterCasingTransformer` for the same reason on DTO-bound query params (PageNumber etc.).

### Generated int64 fields (`number | string`) normalized at the api-layer boundary, not propagated

.NET's OpenAPI generator widens `long`/`int` response fields to a `number | string` union (JS-safe-
integer interop) — e.g. `PagedResultOf*Dto` fields, `SessionDto.id`. Rather than let that union type
leak into views/stores (pagination arithmetic, session-id comparisons), `account-api.ts`/`auth-api.ts`
coerce to plain `number` before returning, keeping `PagedResult<T>`/`SessionDto` as stable hand-written
frontend-only shapes. Everything else in `types.ts` is now a direct re-export of the generated model
type under its existing name — kept the migration transparent to every existing call site.

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

### CORS: explicit allowlist instead of reflect-any-origin

`Program.cs` used `SetIsOriginAllowed(_ => true)` + `AllowCredentials()` — reflects any Origin
back with credentials enabled, which defeats the browser's same-origin protections entirely
(any site can call the API using the victim's cookies). Replaced with `WithOrigins()` bound from
a new `CorsSettings:AllowedOrigins` config array (same required-config pattern as `JwtSettings` —
throws at startup if missing/empty, rather than silently falling back to permissive). Default
dev value is `http://localhost:5173` (frontend's Vite port).
