# Authentication

JWT Bearer tokens are accepted from the `Authorization` header or the `access_token` cookie.

JWT does not carry tenant context — no tenant claim is issued or re-issued on tenant switch.

## Roles

Roles are tenant-scoped only (`TenantRole`: `Owner`, `Member`), resolved per-request via
`X-Tenant-Id` + `ICurrentTenantService`. There is no global account role.

## Headers

- `X-TimeZone` — required. IANA timezone identifier (e.g. `Asia/Ho_Chi_Minh`). Missing/invalid → 400.
- `X-Tenant-Id` — optional. Tenant UUID. If present and the caller is a member, request is scoped
  to that tenant (`ICurrentTenantService.TenantId`/`Role` populated). If absent, invalid, or the
  caller is not a member, no tenant is resolved — this is **not** a 403; tenant-scoped list
  endpoints return empty data, and only mutations that require tenant context reject the request
  (none exist yet in Phase 1).
