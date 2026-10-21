---
paths:
  - "backend/**"
---

# Authentication

Two authentication schemes, both accepted by the default `[Authorize]` policy (`AuthExtensions.cs`):

- **JWT Bearer** — from the `Authorization` header or the `access_token` cookie (`OnMessageReceived`).
- **API key** — the `X-Api-Key` header (`ApiKeyAuthenticationHandler`), issued per organization via
  `/api/admin/api-keys`.

`[AllowAnonymous]` endpoints: `/api/auth/login`, `/api/auth/register`, `/api/auth/verify-email`,
`/api/auth/resend-verification`, `/api/auth/external/{provider}`, `/api/auth/refresh`. `/api/health`
is public too — `HealthController` carries no `[Authorize]`, and there is no fallback policy, so a
controller or action without an `[Authorize]` attribute is public. Every other controller declares one at
class level, except `AuthController`, which authorizes per action — a new action there needs its own attribute.

## Organizations and permissions

Multi-tenant by organization, RBAC per organization. A session is scoped to at most one active
organization, carried as the signed `org_id` JWT claim — never a client-supplied header. Switching
re-issues tokens via `POST /api/auth/switch-organization` (`organizationId: null` returns to the
personal, org-less context).

Each organization has its own roles (`Role`, system kinds `Owner`/`Admin`/`Member` in
`SystemRoleKind`, plus custom ones); a role bundles permissions from the fixed, code-defined catalog
`Permissions`. Endpoints gate with `[Authorize(Policy = Permissions.X)]` (resolved by
`OrganizationPermissionPolicyProvider`) or `AuthorizationPolicies.OrganizationMember` /
`ActiveOrganizationMember` / `SignedInUser`. Effective permissions resolve per request through a
short-TTL cache, not the JWT. The frontend's `meta.permission` route gate is UI-only
(`frontend-conventions.md`).

## Headers

- `X-TimeZone` — required. IANA timezone identifier (e.g. `Asia/Ho_Chi_Minh`). Missing/invalid → 400
  (`UserTimeZoneMiddleware`). Exempt: `/api/health` and `/api/auth/check` (nginx's `auth_request` for the
  user guide forwards only the cookie).

`GET /api/auth/check` (policy `AuthorizationPolicies.SignedInUser`) accepts only a user's JWT — an API key
gets 401 there, unlike every other `[Authorize]` endpoint.

## Temporary lockout

`LoginLockoutSettings` (default: 5 wrong passwords within 15 minutes → locked for 15 minutes). The attempt is reserved in the database *before* the password is checked (`IPasswordAttemptStore`, one column-scoped statement each), so parallel guesses can't exceed the limit; EF never writes the lockout columns on an ordinary account update. The lock is reported (`AccountTemporarilyLocked`) before the password check — reporting it only for a correct password would confirm the guess. Unknown, deactivated and password-less accounts get the generic `InvalidUsernameOrPassword` and count nothing. A successful external (Google/Microsoft) sign-in bypasses the lock and clears the counter.

The current-password check of `PUT /api/profile/password` follows the same scheme and limits on its **own** counter and lock (`PasswordAttemptPurpose.PasswordChange`, reported as `PasswordChangeTemporarilyLocked`): a stolen session can't brute-force the current password, and can't lock the owner out of signing in either — nor do failed sign-ins block a password change. Any new endpoint that verifies a password gets a purpose of its own the same way.
