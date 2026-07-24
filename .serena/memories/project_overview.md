# Project Overview

StarterKit — a generic app-starter monorepo (.NET 10 backend + Vue 3 frontend), repurposed from a
product-specific codebase ("FeedbackHub") into a reusable baseline for new applications.

Not a finished product: no business/domain features are implemented. What's provided is the
infrastructure most apps need, so new projects can start on top of it instead of rebuilding auth
from scratch.

## What's included
- Auth: email+password registration with mandatory email verification (SMTP via MailKit), JWT
  access+refresh tokens (refresh token stored as SHA-256 hash), Google social login (credential
  flow), session management (list/revoke by device).
- Account: CRUD, profile, change password.
- ApiKey: create/manage API keys per account.
- AuditLog: action logging.
- Files: upload/store (local disk today, pluggable via `IStorageProvider`).
- SystemSettings: key/value system configuration.

## What's NOT included
- No multi-tenancy (removed — was Tenant/TenantMembership/TenantRole/X-Tenant-Id, isolated vertical
  slice, cleanly deleted). Single-user/single-account model.
- No global admin/role system — every authenticated account has equal API access.
- No business/domain entities — add your own on top of this base.

## Repo layout
```
backend/    .NET 10 API — Domain -> Application -> Infrastructure -> API (StarterKit.sln)
frontend/   Vue 3 + Vite dashboard
shared/     docs/ + openapi/ (contract placeholder, no codegen wired yet)
plans/      historical dev-log plan files (reference only, not product docs)
```

See `CLAUDE.md`/`AGENTS.md` and `.claude/rules/*.md` for the authoritative, always-loaded rules
(architecture, code conventions, auth, localization, commands, api-contract, frontend-conventions).
`.claude/decisions.md` has the non-obvious architectural decisions still relevant to this baseline.
