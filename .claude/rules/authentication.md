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
