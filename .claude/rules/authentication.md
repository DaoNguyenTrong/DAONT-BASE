---
paths:
  - "backend/**"
---

# Authentication

JWT Bearer tokens are accepted from the `Authorization` header or the `access_token` cookie
(`AuthExtensions.cs`, `OnMessageReceived`). All endpoints require a valid token except
`/api/auth/login`, `/api/auth/register`, `/api/auth/verify-email`, `/api/auth/resend-verification`,
`/api/auth/external/{provider}`, and `/api/auth/refresh`.

## Roles

No multi-tenancy, no admin/global role — single-user/single-account model. Every authenticated
account has equal access to all APIs; add authorization as your specific application needs.

## Headers

- `X-TimeZone` — required. IANA timezone identifier (e.g. `Asia/Ho_Chi_Minh`). Missing/invalid → 400
  (`UserTimeZoneMiddleware`).

## Temporary lockout

`LoginLockoutSettings` (default: 5 wrong passwords within 15 minutes → locked for 15 minutes). The attempt is reserved in the database *before* the password is checked (`IPasswordAttemptStore`, one column-scoped statement each), so parallel guesses can't exceed the limit; EF never writes the lockout columns on an ordinary account update. The lock is reported (`AccountTemporarilyLocked`) before the password check — reporting it only for a correct password would confirm the guess. Unknown, deactivated and password-less accounts get the generic `InvalidUsernameOrPassword` and count nothing. A successful external (Google/Microsoft) sign-in bypasses the lock and clears the counter.

The current-password check of `PUT /api/profile/password` follows the same scheme and limits on its **own** counter and lock (`PasswordAttemptPurpose.PasswordChange`, reported as `PasswordChangeTemporarilyLocked`): a stolen session can't brute-force the current password, and can't lock the owner out of signing in either — nor do failed sign-ins block a password change. Any new endpoint that verifies a password gets a purpose of its own the same way.
