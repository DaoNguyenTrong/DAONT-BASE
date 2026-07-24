# Frontend Conventions (`frontend/`)

Vue 3 + Vite + TypeScript, Pinia, naive-ui, Tailwind, vue-i18n. Package manager is `bun`. See `commands.md` for build/test/format commands.

## File & Naming Conventions

- Components/views: PascalCase, `*View.vue` suffix for route-level views (`frontend/src/views/ProfileView.vue`).
- Composables: kebab-case file, `use*` export (`frontend/src/composables/use-sidebar-menu.ts` → `useSidebarMenu`).
- Stores: kebab-case file, `use*Store` export via Pinia (`frontend/src/stores/auth.ts` → `useAuthStore`).
- API modules: kebab-case, `{resource}-api.ts` (`frontend/src/api/auth-api.ts`).
- Path alias `@/*` maps to `frontend/src/*` (`tsconfig.app.json`) — use it instead of relative `../../` chains.

## Components

Always `<script setup lang="ts">`. `ref`, `computed`, `reactive`, `defineStore`, `useI18n`, etc. are **auto-imported** (`unplugin-auto-import`, `unplugin-vue-components`) — do not manually import them, follow the existing files as the reference for what's auto-imported vs. not.

## Pinia Stores

Setup-store style only (function form, not options form):

```ts
export const useExampleStore = defineStore('example', () => {
  const value = ref<T | null>(null)
  const isReady = computed(() => value.value !== null)

  function setValue(next: T) {
    value.value = next
  }

  return { value, isReady, setValue }
})
```

## API Layer

Each `frontend/src/api/{resource}-api.ts` exports a default object of async functions, not a class:

```ts
import { apiClient } from './client'
import type { ExampleDto, ExampleRequest } from './types'

const getById = async (id: number): Promise<ExampleDto> => {
  const response = await apiClient.get<ExampleDto>(`/api/examples/${id}`)
  return response.data
}

export default { getById /* ... */ }
```

- `apiClient` / `refreshClient` (`frontend/src/api/client.ts`) already handle auth headers, 401 refresh-and-retry queuing, and mapping backend `ProblemDetails` responses to `ApiError`. Never hand-roll another axios instance.
- Types for requests/responses live in `frontend/src/api/types.ts`, hand-maintained today (see `api-contract.md` — no codegen from the backend's OpenAPI spec yet). Keep them in sync with the backend DTOs/requests manually until codegen is wired up.
- In components, catch errors with `error instanceof ApiError` and prefer `error.problem.detail ?? error.problem.title`, falling back to a localized generic string (`t('errors.requestFailed')`) — see `ProfileView.vue` for the pattern.

## Localization

vue-i18n, `useI18n()` in components, keys in `frontend/src/locales/{vi,en}.ts`. Locale switching goes through `useLocaleStore` (`frontend/src/stores/locale-store.ts`), not `i18n.global.locale` directly. See `localization.md` for the cross-cutting rule with backend messages.

## Formatting & Linting

Prettier only (`frontend/.prettierrc.json`: no semicolons, single quotes, printWidth 100) — run `bun run --cwd frontend format`. **There is no ESLint configuration in this project.** Do not add one without confirming with the user first — it would introduce a whole new rule set and CI implication that hasn't been decided on.

## Testing

- Unit: Vitest + `@vue/test-utils`, mock HTTP with `msw`, mock Pinia state with `@pinia/testing`.
- E2E: Playwright (`frontend/e2e`), install browsers once via `bun run --cwd frontend test:e2e:install`.
