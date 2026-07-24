# Frontend Vue/TypeScript Conventions

## File Naming
- Components/views: PascalCase; route-level views use `*View.vue` suffix
- Composables: kebab-case file, `use*` export (`use-sidebar-menu.ts` → `useSidebarMenu`)
- Stores: kebab-case file, `use*Store` export via Pinia
- API modules: `{resource}-api.ts`
- Path alias: `@/*` → `src/*`

## Components
Always `<script setup lang="ts">`. Auto-imports are configured for:
- Vue 3 composables (`ref`, `computed`, `reactive`, `watch`, `defineStore`, etc.)
- `useI18n`, common utils — follow existing files to know what's auto vs explicit

## Pinia Stores — Setup-store style only
```ts
export const useExampleStore = defineStore('example', () => {
  const value = ref<T | null>(null)
  function setValue(next: T) { value.value = next }
  return { value, setValue }
})
```

## API Layer
- `src/api/{resource}-api.ts` exports a default object of async functions (not a class)
- Use `apiClient` / `refreshClient` from `src/api/client.ts` — never create another axios instance
- Types in `src/api/types.ts` — hand-maintained, must be kept in sync with backend wire shapes (camelCase)
- Error handling: `error instanceof ApiError`, prefer `error.problem.detail ?? error.problem.title`

## Tailwind v4 + naive-ui Specificity
Tailwind v4 layers utilities in `@layer utilities`; naive-ui injects unlayered CSS that always wins.
Fix: use `w-full!` (important prefix) on `n-button` and other naive-ui components where Tailwind layout classes don't apply.

## Formatting
Prettier only (`printWidth: 100`, no semicolons, single quotes). No ESLint. Run: `bun run --cwd frontend format`.

## Testing
- Unit: Vitest + `@vue/test-utils`, MSW for HTTP mocking, `@pinia/testing` for store mocking
- Test files mirror `src/` under `tests/`
