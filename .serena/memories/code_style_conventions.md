# Code Style & Conventions

Full detail lives in `.claude/rules/code-conventions.md` (backend) and
`.claude/rules/frontend-conventions.md` (frontend) — always-loaded, treat as authoritative. Summary:

## Backend (C#)
- Avoid `var` — explicit types everywhere.
- PascalCase: classes/methods/properties. camelCase: locals/params, primary-constructor-derived
  private fields. `_camelCase`: private fields in traditional-constructor classes.
- Async methods suffixed `Async`, always thread `CancellationToken ct` through.
- Nullable reference types on; prefer `?.`/`??`/`?? throw` over `!`.
- **Entity pattern**: private constructor + static `Create(XxxParams p)` factory + `Update(XxxParams p)`.
  All domain validation lives in `Update`. Never construct entities via EF/Mapperly directly.
- Mapperly: `EntityMapper.ToDto(entity)`, `request.ToParams()` extension methods.
- EF Core is NoTracking-global — `repository.Update(entity)` required to re-attach detached entities.

## Frontend (Vue/TS)
- `<script setup lang="ts">` only. Auto-imported: ref/computed/reactive/defineStore/useI18n etc. —
  don't manually import, follow existing files for what's auto-imported.
- Pinia: setup-store function form only, not options form.
- API layer: `frontend/src/api/{resource}-api.ts` exports a default object of async functions
  (not a class), uses shared `apiClient`/`refreshClient` from `client.ts` — never hand-roll axios.
- Naming: PascalCase `*View.vue` for route views, kebab-case `use-*.ts` composables,
  kebab-case `{resource}-api.ts` API modules. Path alias `@/*` -> `frontend/src/*`.
- Prettier only (no semicolons, single quotes, printWidth 100) — **no ESLint in this project**,
  don't add one without confirming with the user first.
