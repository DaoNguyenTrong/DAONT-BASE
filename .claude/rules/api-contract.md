# API Contract (backend ↔ frontend)

Read this when changing a backend endpoint, request/response DTO, or header contract that the frontend consumes.

## Current State

`shared/openapi/openapi.json` is the committed source of truth, exported from `backend/src/StarterKit.API`'s OpenAPI document at build time (no running server/DB required). The frontend generates its types and typed request functions from it with `orval` — `frontend/src/api/generated/**` is generated output, never hand-edited, and **gitignored** (same convention as `frontend/src/typings/`): it regenerates automatically via a `postinstall` script on every `bun install`, so it's always freshly derived from whatever `openapi.json` is checked out — no risk of committed-generated-code drifting from its source.

### Regenerating after a backend contract change

```bash
# 1. Backend: re-export the spec (writes shared/openapi/openapi.json)
dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true

# 2. Frontend: regenerate types + client from the updated spec
bun run --cwd frontend codegen
```

`OpenApiGenerateDocumentsOnBuild` defaults to `false` on this project (see `StarterKit.API.csproj`) — generating the doc costs ~5-9s via a design-time host, so it's opt-in per the command above rather than running on every plain `dotnet build`.

## How the generated client is wired into the app

There is no hand-written `{resource}-api.ts` wrapper layer — views/components/composables call `frontend/src/api/generated/**` (`mode: 'tags-split'`, one file per controller tag, each exporting a `get*()` factory e.g. `getAccounts()` → `{ accountsGetAll, accountsCreate, ... }`) directly. All generated requests route through `frontend/src/api/mutator.ts`, a thin wrapper around the existing `apiClient` axios instance — so every generated call automatically gets `apiClient`'s interceptors (auth headers, locale, the 401-refresh-and-retry queue) for free, no per-call special-casing needed.

Side effects the spec can't express live in the Pinia store that owns the relevant state, not at the call site:

- **`frontend/src/stores/auth.ts`** wraps `getAuth()` in store actions (`login`, `register`, `verifyEmail`, `resendVerification`, `externalLogin`, `logout`, `getSessions`, `revokeSession`, `revokeOtherSessions`) — `login`/`verifyEmail`/`externalLogin` call `setAuth(response)` internally after a successful response, so any call site doing `useAuthStore().login(data)` gets the side effect for free instead of remembering to call it.
- **`useAuthStore().refreshToken`** is kept fully hand-written inside the store, bypassing the generated client — it must go through `refreshClient` (a separate axios instance with no 401-retry interceptor), so a failed silent refresh on app boot can't recurse into `apiClient`'s own refresh-and-retry logic.
- Resources with no cross-cutting state (Profile, Health, Accounts) have no store wrapper — components call the generated `get*()` factory directly (see `ProfileDialog.vue`, `use-health-status.ts`, `AccountsView.vue`).

## Known generated-type quirks (and how they're handled)

- **int64/int32 response fields become `number | string`** (the .NET OpenAPI generator widens them to a string-pattern union for JS-safe-integer interop) — e.g. `PagedResultOf*Dto.totalCount/pageNumber/pageSize/totalPages`, `SessionDto.id`. Normalize to plain `number` at the point of use before it reaches shared state: `frontend/src/lib/paged-result.ts`'s `toPagedResult()` for any paginated list (see `AccountsView.vue`), inline `Number(...)` for `SessionDto.id` (see `stores/auth.ts`'s `getSessions`). Don't propagate the union type into views.
- **`frontend/src/api/types.ts`** re-exports generated model types under their existing names (`export type { AccountDto as Account } from './generated/model/accountDto'`, etc.) rather than hand-declaring them. `ProblemDetails`, `ApiError`, and `PagedResult<T>` stay hand-written (frontend-only shapes, no 1:1 backend schema).
- **`vite.config.ts`'s `AutoImport` `dirs`** excludes `src/api/generated/**` (`'!src/api/generated/**'`) — otherwise the generated per-controller factory functions (`getAuth()`, `getAccounts()`, etc.) would auto-import globally, colliding with `types.ts`'s re-exported type names of the same identifier. Each generated factory is called once per consuming file/store (`const client = getAuth()`) and reused for all its methods, not re-invoked per call.

## Rule: Changing a Backend Endpoint or DTO

Whenever you change a controller route, request/response DTO shape, status code, or error contract in `backend/src/StarterKit.API` or `backend/src/StarterKit.Application`:

1. Regenerate (see command above) — this alone updates `frontend/src/api/generated/**` and surfaces any breakage as TypeScript errors at every call site.
2. If the change affects error responses, remember the frontend maps errors through `ApiError`/`ProblemDetails` (`frontend/src/api/client.ts`) — check `toProblemDetails`/`toApiError` still handle the shape.
3. If the change affects headers (`X-Tenant-Id`, `X-TimeZone`, `Authorization`/`access_token` cookie — see `authentication.md`), check `frontend/src/api/client.ts` still sends them correctly (headers aren't part of the generated contract, they're applied by `apiClient`'s interceptors).
4. If a new controller/resource needs cross-cutting side effects on success (analogous to `setAuth`), add them as a Pinia store action wrapping the generated call — don't reintroduce a per-resource `{resource}-api.ts` wrapper file.

## Controller Route Casing

All controllers declare an explicit lowercase route (`[Route("api/accounts")]`, not `[Route("api/[controller]")]`) — kept consistent on purpose. ASP.NET Core's routing is case-insensitive so this was never a runtime issue, but the OpenAPI spec captures the *declared* casing verbatim, and codegen reproduces it exactly. A PascalCase-via-`[controller]`-token route would make the generated client send PascalCase paths, inconsistent with every hand-written caller (and test mock) in this codebase, which has always used lowercase. Same reasoning applies to query parameters bound from a shared DTO (e.g. paging `PageNumber`/`PageSize`/`Search`) — `QueryParameterCasingTransformer` (`backend/src/StarterKit.API/OpenApi/`) normalizes the declared casing to camelCase in the spec for the same reason.
