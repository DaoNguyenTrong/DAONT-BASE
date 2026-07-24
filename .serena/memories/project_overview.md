# FEEDBACK-HUB — Project Overview

Monorepo with three top-level trees:

| Path | Stack |
|---|---|
| `backend/` | .NET 10, Clean Architecture (Domain → Application → Infrastructure → API) |
| `frontend/` | Vue 3 + Vite + TypeScript, Pinia, naive-ui, Tailwind v4, vue-i18n |
| `shared/` | `docs/`, `openapi/` (placeholder — no codegen yet) |

## Purpose
Multi-tenant feedback hub SaaS. Accounts authenticate via username/password or Google OAuth. Refresh tokens are stored as SHA-256 hashes; each token row = one session (supports session management UI).

## Key Architecture Points
- **Backend**: Clean Architecture. Entity factory pattern (`static Create(XxxParams)` + `Update(XxxParams)`). EF Core with NoTracking global. JWT in cookie (`access_token`) + refresh token in cookie (`refresh_token`). Tenant context via `X-Tenant-Id` header.
- **Frontend**: Vue 3 `<script setup lang="ts">`. Auto-imports via `unplugin-auto-import` / `unplugin-vue-components`. Pinia setup-store style. API layer in `src/api/{resource}-api.ts`. Types in `src/api/types.ts` (hand-maintained vs backend DTOs). `@/*` alias → `src/*`.
- **Localization**: Backend uses `.resx` files + `DomainMessages.cs` + `ApplicationMessages.cs`. Frontend uses `vue-i18n` with `src/locales/{vi,en}.ts`. Both `vi` and `en` must always be kept in sync.
