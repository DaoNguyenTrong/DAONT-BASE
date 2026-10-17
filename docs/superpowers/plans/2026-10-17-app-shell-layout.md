# App Shell Layout (header, sidebar, menu modes) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rebuild the app shell so users pick one of 5 menu modes × 2 menu colors × light/dark/system from an appearance panel, with a modern header (title + breadcrumb, Ctrl K palette, help, bell, ⚙, avatar menu) and a navigation-only sidebar with menu groups.

**Architecture:** Pure persistence/model code lives in `src/lib/` (layout preferences, theme, palette registry) and `src/layouts/` (menu model); one setup store (`useLayoutStore`) owns mode/theme/transient UI state; the shell is split into focused components (`AppSidebar` for vertical modes, `AppTopMenu` for horizontal, `AppHeader`, `UserMenu`, `AppearanceSettings`, `CommandPalette`) that `AppLayout` arranges by `menuMode`. Everything persists in `localStorage` only.

**Tech Stack:** Vue 3.5 `<script setup lang="ts">`, Pinia setup stores, naive-ui 2.44, Tailwind v4 tokens (`primary-*`, `surface-*`), vue-i18n, `@vicons/tabler`, Vitest + @vue/test-utils + MSW, Playwright (E2E + guide capture).

**Spec:** `docs/superpowers/specs/2026-10-17-app-shell-layout-design.md` (Vietnamese; section numbers below refer to it — read it alongside this plan).

## Global Constraints

- Work on branch `feature/app-shell-layout` cut from `dev`. Stage files by explicit path; conventional commits; never `--no-verify`. Do **not** commit the spec or this plan unless the user says so.
- Desktop breakpoint is Tailwind `lg` (64rem/1024px); logic branches use `useMediaQuery(BELOW_LG_QUERY)` from `lib/responsive.ts`. Below `lg` every mode falls back to the left `n-drawer` (280px).
- Menu modes: `'static' | 'slim' | 'slim-plus' | 'drawer' | 'horizontal'`. Menu themes: `'brand' | 'transparent'`. Defaults: `static` + `brand`, `drawerPinned=false`, `staticInactive=false`.
- Storage keys: `layout-menu-mode`, `layout-menu-theme`, `layout-drawer-pinned`, `layout-static-inactive`. One-time migration from legacy `sidebar-mode`: `full`→`static`, `minimal`→`slim`, then remove `sidebar-mode`. Every storage access wrapped in try/catch; garbage → default.
- Theme preference: `'light' | 'dark' | 'system'`, key `theme-preference` (unchanged), default `light`. Font size: existing `lib/font-size.ts`, 14–20px.
- Sizes: static sidebar 16rem, slim rail 4rem, slim-plus rail 6rem, header `h-16`, settings drawer 320px.
- Colors (§4.1): brand light `bg-primary-600` + white text, active `bg-white/15`; brand dark `bg-surface-900`; transparent light `bg-surface-100`, text `surface-600`, group titles `surface-400`, active white pill + `text-primary-600`; transparent dark `bg-surface-950`, active `bg-surface-800 text-primary-200`. Brand color stays `#5e4c9e`.
- Touch targets: keep `max-lg:h-11 max-lg:w-11` on header icon buttons and `pointer-coarse:size-10` patterns.
- Every user-facing string is a key in both `src/locales/vi.ts` and `src/locales/en.ts`; tests assert via `i18n.global.t(...)`, never literal copy.
- Conventions: setup stores only; rely on auto-imports (`src/lib/**`, `src/composables/**`, `src/stores/**` are auto-imported — pick names that don't collide); Prettier only (`bun run --cwd frontend format`); no ESLint.
- `AppControls.vue` stays unchanged in behavior for `LoginView`, `RegisterView`, `NotFoundView`, `GuideAuthView`, `ServerErrorScreen`; only `AppHeader` stops using it.
- Per-task verification: `bun run --cwd frontend test:run <files>`; before the final task's commit, the full gate (Task 12).

## Review Focus

1. **Storage that throws** (Safari private mode, blocked site data): `localStorage.getItem/setItem` raising must leave the shell on defaults, never a blank page. → test in Task 2.
2. **Crossing the `lg` breakpoint with something open** (mobile drawer open, drawer-mode sidebar hovered) then widening/narrowing the window: the mobile drawer closes at ≥ `lg`, hover state resets. → test in Task 8.
3. **Permissions changing while the palette is open** (token refresh, org switch in another tab): results must not offer a page the guard would bounce. → test in Task 6.
4. **Names that don't fit the happy path** in the avatar: no `name`, single-word, Vietnamese diacritics, very long — initials and label must stay sane. → test in Task 4.
5. **Ctrl K outside the shell / twice:** the shortcut is live only while `AppLayout` is mounted (not on login), and pressing it with the palette open doesn't stack dialogs. → test in Task 6.

---

### Task 1: Theme preference gains `system`

**Files:**

- Modify: `frontend/src/lib/theme.ts`
- Modify: `frontend/src/composables/use-theme-preference.ts`
- Test: `frontend/tests/unit/lib/theme.test.ts`, `frontend/tests/components/AppControls.test.ts`

**Interfaces:**

- Produces (lib): `type ThemePreference = 'light' | 'dark' | 'system'`; `type EffectiveTheme = 'light' | 'dark'`; `getStoredThemePreference(): ThemePreference | null`; `getThemeSetting(): ThemePreference` (stored ?? `'light'`); `resolveTheme(pref: ThemePreference): EffectiveTheme`; `applyThemePreference(pref: ThemePreference): EffectiveTheme`; `getThemePreference(): EffectiveTheme` (unchanged meaning: what the DOM shows); `setThemePreference(pref: ThemePreference): EffectiveTheme`; `toggleThemePreference(): EffectiveTheme` (stores the explicit opposite of the effective theme); `initializeThemePreference(): EffectiveTheme`; `watchSystemTheme(onChange: (theme: EffectiveTheme) => void): () => void`.
- Produces (composable): `useThemePreference(): { isDark: Ref<boolean>; preference: Ref<ThemePreference>; toggle(): void; setPreference(p: ThemePreference): void }`.

- [ ] **Step 1: Write failing tests** in `theme.test.ts` (stub `matchMedia` with `vi.stubGlobal`, a fake list whose `change` listeners the test can fire):

```ts
describe('system preference', () => {
  it('stores system and applies the OS scheme', () => {
    stubPrefersDark(true)
    expect(setThemePreference('system')).toBe('dark')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('system')
    expect(document.documentElement.classList.contains('dark')).toBe(true)
  })
  it('falls back to light when matchMedia is missing', () => {
    vi.stubGlobal('matchMedia', undefined)
    expect(resolveTheme('system')).toBe('light')
  })
  it('follows an OS change only while the setting is system', () => {
    const media = stubPrefersDark(false)
    const seen: string[] = []
    const stop = watchSystemTheme((t) => seen.push(t))
    setThemePreference('system')
    media.fire(true)
    expect(document.documentElement.classList.contains('dark')).toBe(true)
    setThemePreference('light')
    media.fire(false)
    media.fire(true)
    expect(document.documentElement.classList.contains('dark')).toBe(false)
    expect(seen).toEqual(['dark'])
    stop()
  })
  it('toggle from system stores the explicit opposite of what is shown', () => {
    stubPrefersDark(true)
    setThemePreference('system')
    expect(toggleThemePreference()).toBe('light')
    expect(localStorage.getItem(THEME_STORAGE_KEY)).toBe('light')
  })
  it('initialize applies a stored system setting', () => {
    stubPrefersDark(true)
    localStorage.setItem(THEME_STORAGE_KEY, 'system')
    expect(initializeThemePreference()).toBe('dark')
  })
})
```

Also extend the existing `getStoredThemePreference` cases: `'system'` is returned as valid.

- [ ] **Step 2: Run** `bun run --cwd frontend test:run tests/unit/lib/theme.test.ts` — Expected: FAIL (`watchSystemTheme`/`resolveTheme` not exported, `'system'` rejected).

- [ ] **Step 3: Implement** the lib signatures above. Media query `'(prefers-color-scheme: dark)'`; `watchSystemTheme` adds one `change` listener per call and re-applies only when `getThemeSetting() === 'system'`. Composable: module-level `preference = ref(getThemeSetting())`, `isDark = ref(getThemePreference() === 'dark')`, and one module-level `watchSystemTheme(t => isDark.value = t === 'dark')`.

- [ ] **Step 4: Add to `AppControls.test.ts`**: `it('switches to the opposite of the shown theme when the setting is system')` — set `'system'` with OS dark, click the theme button, expect `localStorage['theme-preference'] === 'light'`.

- [ ] **Step 5: Run** both test files — Expected: PASS. Run `bun run --cwd frontend type-check` — Expected: no errors (callers of `ThemePreference` still compile).

- [ ] **Step 6: Commit** `feat(frontend): add a system theme preference` (theme.ts, use-theme-preference.ts, both tests).

---

### Task 2: Layout preferences + `useLayoutStore`

**Files:**

- Create: `frontend/src/lib/layout-preferences.ts`
- Create: `frontend/src/stores/layout-store.ts`
- Test: `frontend/tests/unit/lib/layout-preferences.test.ts`, `frontend/tests/unit/stores/layout-store.test.ts`

(`stores/sidebar-store.ts` stays until Task 8 removes it; nothing uses the new store before then.)

**Interfaces:**

- Produces (lib): `type MenuMode = 'static' | 'slim' | 'slim-plus' | 'drawer' | 'horizontal'`; `type MenuTheme = 'brand' | 'transparent'`; `MENU_MODES: readonly MenuMode[]` (that order); `MENU_THEMES: readonly MenuTheme[]`; `interface LayoutPreferences { menuMode: MenuMode; menuTheme: MenuTheme; drawerPinned: boolean; staticInactive: boolean }`; `DEFAULT_LAYOUT_PREFERENCES`; `LAYOUT_STORAGE_KEYS = { menuMode: 'layout-menu-mode', menuTheme: 'layout-menu-theme', drawerPinned: 'layout-drawer-pinned', staticInactive: 'layout-static-inactive' } as const`; `LEGACY_SIDEBAR_MODE_KEY = 'sidebar-mode'`; `readLayoutPreferences(): LayoutPreferences` (runs the migration); `writeLayoutPreference<K extends keyof LayoutPreferences>(key: K, value: LayoutPreferences[K]): void`.
- Produces (store `useLayoutStore`): state `menuMode`, `menuTheme`, `drawerPinned`, `staticInactive` (persisted); `mobileOpen`, `drawerExpanded`, `settingsOpen`, `paletteOpen`, `profileOpen` (session only, `false`). Getters: `isRail: boolean` (`slim` | `slim-plus` | `drawer` && !drawerPinned), `railWidth: '4rem' | '6rem'`. Actions: `setMenuMode(m)`, `setMenuTheme(t)`, `toggleStaticSidebar()`, `setDrawerPinned(v)`, `expandDrawer()`, `collapseDrawer()`, `openMobile()`, `closeMobile()`, `openSettings()`, `closeSettings()`, `openPalette()`, `closePalette()`, `openProfile()`, `closeProfile()`.

- [ ] **Step 1: Write failing lib tests** (`describe('readLayoutPreferences')`):
  - `it('returns the defaults when nothing is stored')` → `toEqual({ menuMode: 'static', menuTheme: 'brand', drawerPinned: false, staticInactive: false })`.
  - `it.each([['full', 'static'], ['minimal', 'slim']])('migrates legacy sidebar-mode %s to %s')` → `menuMode` matches, `localStorage['layout-menu-mode']` written, `localStorage['sidebar-mode']` is `null`.
  - `it('ignores the legacy key once the new key exists')` → new `slim-plus` + legacy `full` gives `slim-plus`.
  - `it('falls back to the default for garbage values')` → `'sideways'`, `'neon'`, `'yes'` → defaults.
  - `it('returns defaults when storage throws')` → `vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => { throw new Error('blocked') })`; no throw, defaults. Same for `setItem` throwing inside `writeLayoutPreference` (no throw).
- [ ] **Step 2: Run** `bun run --cwd frontend test:run tests/unit/lib/layout-preferences.test.ts` — Expected: FAIL (module missing).
- [ ] **Step 3: Implement** `lib/layout-preferences.ts` (booleans stored as `'true'`/`'false'`).
- [ ] **Step 4: Write failing store tests** (`setupTestPinia()` like `sidebar-store.test.ts`):
  - `it('restores persisted preferences at creation')`.
  - `it('setMenuMode persists and clears session state')` → after `expandDrawer()` + `setMenuMode('horizontal')`: stored `'horizontal'`, `drawerExpanded === false`.
  - `it('isRail is true for slim, slim-plus and an unpinned drawer only')` (table over modes × pinned).
  - `it('railWidth is 6rem for slim-plus and 4rem otherwise')`.
  - `it('toggleStaticSidebar flips and persists staticInactive')`.
- [ ] **Step 5: Implement** `stores/layout-store.ts`; **run** both test files — Expected: PASS.
- [ ] **Step 6: Commit** `feat(frontend): add layout preferences and the layout store`.

---

### Task 3: Menu groups and the menu model

**Files:**

- Create: `frontend/src/layouts/menu-groups.ts`
- Modify: `frontend/src/layouts/sidebar-menu.ts`, `frontend/src/composables/use-sidebar-menu.ts`, `frontend/src/router/index.ts:16-20` (RouteMeta)
- Test: `frontend/tests/unit/composables/use-sidebar-menu.test.ts`

**Interfaces:**

- Produces (`menu-groups.ts`): `interface MenuGroup { id: string; labelKey: string; icon: Component; order: number }`; `export const MENU_GROUPS: MenuGroup[] = []` with a doc comment telling forks to add groups here.
- Produces (`RouteMeta.sidebar`): adds `group?: string` (a `MenuGroup.id`).
- Produces (`sidebar-menu.ts`): keep `SidebarMenuItem` as is; add `interface SidebarMenuGroup { kind: 'group'; id: string; labelKey: string; icon: Component; items: SidebarMenuItem[] }`, `type SidebarMenuLink = SidebarMenuItem & { kind: 'item' }`, `type SidebarMenuEntry = SidebarMenuGroup | SidebarMenuLink`; `interface FlatMenuLink { labelKey: string; icon: Component; routeName: string; trailKeys: string[] }`; `flattenMenuLinks(entries: SidebarMenuEntry[]): FlatMenuLink[]` (leaves only, `trailKeys` = labelKeys of group/parents); `containsRoute(entry: SidebarMenuEntry | SidebarMenuItem, routeName: string | undefined): boolean`.
- Produces (`use-sidebar-menu.ts`): `buildMenuEntries(routes: readonly RouteRecordRaw[], hasPermission: (code: string) => boolean, groups: readonly MenuGroup[]): SidebarMenuEntry[]` (exported); `useSidebarMenu(groups: readonly MenuGroup[] = MENU_GROUPS): ComputedRef<SidebarMenuEntry[]>`.

- [ ] **Step 1: Update existing tests** to the new shape (results are entries: map with `kind === 'item' ? e.routeName : e.id`) and **add failing tests**:
  - `it('gathers routes that name the same group under one entry, ordered by item order')`.
  - `it('orders groups and ungrouped items together by order')` → item `order: 1`, group `order: 2`, item `order: 3` → `['a', 'admin', 'c']`.
  - `it('drops a group left empty after the permission filter')`.
  - `it('treats an unknown group id as ungrouped and warns')` → `vi.spyOn(console, 'warn')` called once; item appears top-level.
  - `it('keeps meta without group working as before')` (legacy fork meta).
  - `describe('flattenMenuLinks')`: `it('returns leaves with their group and parent trail')` → `trailKeys` equals `['nav.groups.admin', 'nav.reports']` for a grandchild.
  - `describe('containsRoute')`: true for a group whose nested child matches, false otherwise.
- [ ] **Step 2: Run** the file — Expected: FAIL.
- [ ] **Step 3: Implement.** Group order comes from `MenuGroup.order`, ungrouped item order from `meta.sidebar.order ?? 0`; warn only when `import.meta.env.DEV`. Update the two existing consumers (`AppSidebar.vue` `v-for` over `sidebarMenu`) to render `entry.kind === 'group' ? entry.items : [entry]` flattened so the current UI still works until Task 8 (no visual change; groups list is empty in base).
- [ ] **Step 4: Run** `bun run --cwd frontend test:run tests/unit/composables/use-sidebar-menu.test.ts tests/layouts` — Expected: PASS. `type-check` — PASS.
- [ ] **Step 5: Commit** `feat(frontend): add menu groups to the sidebar menu model`.

---

### Task 4: Organization list + user (avatar) menu

**Files:**

- Create: `frontend/src/components/OrganizationList.vue` (logic + markup moved out of `OrganizationSwitcher.vue`)
- Create: `frontend/src/layouts/UserMenu.vue`
- Create: `frontend/src/lib/initials.ts`
- Modify: `frontend/src/components/OrganizationSwitcher.vue` (renders `<OrganizationList>` inside its popover until Task 8 deletes it), `src/locales/{vi,en}.ts`
- Test: `frontend/tests/components/OrganizationList.test.ts`, `frontend/tests/layouts/UserMenu.test.ts`, `frontend/tests/unit/lib/initials.test.ts`

**Interfaces:**

- Produces: `OrganizationList` — no props; emits `done` (after a no-op pick or "manage"); on mount fetches `organizationsStore.fetchMine()` if `!loaded`; keeps the `Loader`/`Check` states and `useOrganizationSwitch()` full-reload; testids `organization-list`, `organization-list-manage`. "Manage" pushes `{ name: 'organizations' }` and calls `useLayoutStore().closeMobile()`.
- Produces: `initialsOf(name: string | null | undefined): string` — first letter of the first and last words, uppercased, diacritics kept (`'Nguyễn Trọng'`→`'NT'`, `'admin'`→`'A'`, `'  '`/`null`→`'?'`).
- Produces: `UserMenu` — no props. Trigger testid `app-header-user-menu`: initials circle + (≥`xl`) name and current org label (`auth.organizationName ?? t('organizations.personalWorkspace')`), each `truncate` with `title`. Panel (`n-popover`, `placement="bottom-end"`, `role="menu"`): header (initials, `account.name || account.username || t('common.user')`, email); `<OrganizationList @done="close">`; "Hồ sơ cá nhân" (testid `user-menu-profile`, calls `layout.openProfile()`; `UserMenu` renders `<ProfileDialog v-model:visible="layout.profileOpen">`); language rows Tiếng Việt / English with a ✓ (testid `user-menu-locale-vi|en`, `localeStore.setLocale`); "Đăng xuất" in error color (testid `user-menu-logout`, same `requestConfirmationNaive` → `useAuthStore().logout()` as `AppSidebar.handleLogout`).
- Locale keys added (vi / en): `layout.userMenu.open` "Tài khoản" / "Account"; `layout.userMenu.language` reuse `common.language`; `layout.userMenu.vietnamese` "Tiếng Việt" / "Tiếng Việt"; `layout.userMenu.english` "English" / "English".

- [ ] **Step 1: Failing `initials.test.ts`** — `it.each` over the four cases above plus `'Lê   Văn   An'`→`'LA'`.
- [ ] **Step 2: Implement `initialsOf`; run — PASS.**
- [ ] **Step 3: Move `OrganizationSwitcher` behavior tests** that are not about placement into `OrganizationList.test.ts` and add: `it('switches organization through the API and reloads')` (MSW `server.use` on the switch endpoint, `vi.mock('@/lib/session-exit')` or the existing pattern in the switcher tests), `it('emits done when the current organization is picked')`, `it('goes to organizations on manage')`.
- [ ] **Step 4: Failing `UserMenu.test.ts`** (`renderComponent`, seed `auth` via `initialState`):
  - `it('shows initials, name and the current organization on the trigger')`.
  - `it('falls back to the username, then to the generic user label')`.
  - `it('asks for confirmation before logging out')` → click `user-menu-logout`, expect `requestConfirmationNaive` called with `header: i18n.global.t('auth.logout')`.
  - `it('switches the locale from the menu')` → click `user-menu-locale-en`, `storeIn(pinia, useLocaleStore).currentLocale === 'en'`.
  - `it('opens the profile dialog')`.
- [ ] **Step 5: Implement** `OrganizationList.vue`, `UserMenu.vue`, slim `OrganizationSwitcher.vue` down to a trigger + `<OrganizationList>`; add locale keys; **run** the three test files plus `tests/components/OrganizationSwitcher.test.ts` — Expected: PASS.
- [ ] **Step 6: Commit** `feat(frontend): add the user menu and extract the organization list`.

---

### Task 5: Appearance settings panel

**Files:**

- Create: `frontend/src/layouts/AppearanceSettings.vue`, `frontend/src/layouts/MenuModePreview.vue` (tiny CSS/SVG thumbnail per mode, prop `mode: MenuMode`)
- Modify: `src/locales/{vi,en}.ts`
- Test: `frontend/tests/layouts/AppearanceSettings.test.ts`

**Interfaces:**

- Consumes: `useLayoutStore` (Task 2: `settingsOpen`, `closeSettings`, `menuMode`, `setMenuMode`, `menuTheme`, `setMenuTheme`), `useThemePreference` (Task 1: `preference`, `setPreference`), `getFontSize/setFontSize/MIN_FONT_SIZE/MAX_FONT_SIZE/DEFAULT_FONT_SIZE`, `useMediaQuery(BELOW_LG_QUERY)`.
- Produces: `AppearanceSettings` — `n-drawer` right, width 320, `v-model:show` bound to `settingsOpen`. Sections in order: color mode (3 segments, testids `appearance-theme-light|dark|system`), menu type (5 cards, testids `appearance-mode-<mode>`, `aria-pressed`), menu color (testids `appearance-menu-theme-brand|transparent`), font size (A−, value `{n}px`, A+, reset; reuse `common.decreaseFontSize` etc.). Below `lg`, the menu-type group is `aria-disabled`/dimmed with the note but still clickable and persisted.
- Locale keys (vi / en): `layout.settings.title` "Cài đặt giao diện" / "Appearance settings"; `layout.settings.colorMode` "Chế độ màu" / "Color mode"; `layout.settings.light` "Sáng" / "Light"; `layout.settings.dark` "Tối" / "Dark"; `layout.settings.system` "Theo hệ thống" / "System"; `layout.settings.menuMode` "Kiểu menu" / "Menu type"; `layout.settings.menuModeWideOnly` "Áp dụng trên màn hình rộng" / "Applies on wide screens"; `layout.settings.menuTheme` "Màu menu" / "Menu color"; `layout.menuModes.{static,slim,slimPlus,drawer,horizontal}` "Static" / "Slim" / "Slim+" / "Drawer" / "Horizontal" (both locales); `layout.menuThemes.brand` "Thương hiệu" / "Brand"; `layout.menuThemes.transparent` "Trong suốt" / "Transparent".

- [ ] **Step 1: Failing tests:**
  - `it('applies and stores each menu mode')` → click `appearance-mode-slim-plus`; store `menuMode === 'slim-plus'`; card has `aria-pressed="true"`.
  - `it('changes the menu color')`.
  - `it('sets the system color mode')` → `localStorage['theme-preference'] === 'system'`.
  - `it('steps the font size within 14 to 20')` → at 20 the A+ button is disabled.
  - `it('notes that the menu type applies on wide screens below lg')` → stub `matchMedia` matches; text `i18n.global.t('layout.settings.menuModeWideOnly')` present.
- [ ] **Step 2: Run** — FAIL. **Step 3: Implement.** **Step 4: Run** — PASS.
- [ ] **Step 5: Commit** `feat(frontend): add the appearance settings panel`.

---

### Task 6: Command palette (Ctrl/⌘ K)

**Files:**

- Create: `frontend/src/lib/command-palette.ts`, `frontend/src/layouts/CommandPalette.vue`
- Modify: `src/locales/{vi,en}.ts`
- Test: `frontend/tests/unit/lib/command-palette.test.ts`, `frontend/tests/layouts/CommandPalette.test.ts`

**Interfaces:**

- Produces (lib): `interface PaletteAction { id: string; labelKey: string; icon: Component; keywords?: string[]; permission?: string; run: () => void | Promise<void> }`; `registerPaletteAction(action: PaletteAction): () => void` (returns unregister; re-registering an `id` replaces it); `usePaletteActions(): Readonly<Ref<readonly PaletteAction[]>>`; `interface PaletteResult { kind: 'page' | 'action'; id: string; labelKey: string; icon: Component; trailKeys: string[] }`; `filterPaletteResults(results: PaletteResult[], query: string, translate: (key: string) => string): PaletteResult[]` (empty query → all; match on translated label + trail + keywords via `matchesSearchText` from `lib/search-text.ts`; pages first).
- Produces (component): `CommandPalette` — `n-modal` bound to `layout.paletteOpen`; input autofocus; ↑/↓ moves `aria-activedescendant`, Enter runs (page → `router.push({ name })`, action → `run()`), Esc closes. Pages from `flattenMenuLinks(useSidebarMenu().value)` (Task 3), so permission-filtered and reactive. Base actions registered inside the component's setup (unregistered on unmount): open appearance settings (`layout.openSettings()`), profile (`layout.openProfile()`), switch language (`localeStore.toggleLocale()`), logout (same confirmation). Exposes the global key handler: `useEventListener`-style `keydown` on `window` registered in `onMounted`, removed in `onUnmounted`; `(e.ctrlKey || e.metaKey) && e.key.toLowerCase() === 'k'` → `preventDefault()`; opens only if not already open. (`MarkdownEditor` has no Ctrl K binding — verified — so no exception is needed.)
- Locale keys (vi / en): `layout.palette.trigger` "Đi tới trang…" / "Go to page…"; `layout.palette.placeholder` "Nhập tên trang hoặc hành động" / "Type a page or action"; `layout.palette.empty` "Không có kết quả" / "No results"; `layout.palette.pages` "Trang" / "Pages"; `layout.palette.actions` "Hành động" / "Actions"; `layout.palette.switchLanguage` "Đổi ngôn ngữ" / "Switch language".

- [ ] **Step 1: Failing lib tests:** `it('matches without accents')` ("to chuc" finds `nav.organizations` → "Tổ chức"), `it('matches the parent trail')`, `it('lists pages before actions')`, `it('replaces an action registered twice with the same id')`, `it('unregisters an action')`.
- [ ] **Step 2: Run — FAIL; implement lib; run — PASS.**
- [ ] **Step 3: Failing component tests** (mount inside a host that renders `<CommandPalette/>` with a router whose `/` children carry `meta.sidebar`):
  - `it('opens on Ctrl K and on Meta K')`.
  - `it('does not stack a second dialog when Ctrl K is pressed while open')` → one `.n-modal` / one input.
  - `it('stops listening once unmounted')` → after `wrapper.unmount()`, Ctrl K leaves `paletteOpen` false.
  - `it('never offers a page the user lacks permission for, even after permissions change while open')` → open, set `auth.permissions = []`, gated page disappears.
  - `it('navigates to the highlighted page on Enter')`.
  - `it('runs a fork-registered action')` → `registerPaletteAction({ id: 'x', ... run: spy })`, type its label, Enter → `spy` called.
- [ ] **Step 4: Implement; run — PASS. Step 5: Commit** `feat(frontend): add the Ctrl K command palette`.

---

### Task 7: New header

**Files:**

- Modify: `frontend/src/layouts/AppHeader.vue`, `src/locales/{vi,en}.ts`
- Test: `frontend/tests/layouts/AppHeader.test.ts`

**Interfaces:**

- Consumes: `useLayoutStore` (`menuMode`, `toggleStaticSidebar`, `openMobile`, `openSettings`, `openPalette`), `UserMenu` (Task 4), `NotificationBell`, existing breadcrumb logic.
- Produces: `AppHeader` — prop `variant?: 'bar' | 'actions'` (default `'bar'`). `'bar'`: full header (☰ · title+crumbs · actions). `'actions'`: only the right-hand cluster (palette trigger, help, bell, ⚙, avatar) for Task 10's top menu. Testids: `app-header-toggle`, `app-header-title`, `app-header-palette`, `app-header-settings`, keep `app-header-user-guide` and `app-header-crumb-text`.
- Rules (§5.1): ☰ shows below `lg` always (opens mobile drawer) and at ≥ `lg` only when `menuMode === 'static'` (toggles `staticInactive`); title = label of the deepest `breadcrumbKey`; crumbs above it are the earlier levels (links), hidden below `lg` and on home; palette trigger is a pill with `Ctrl K` (`⌘ K` when `navigator.platform`/`userAgentData` says mac) and an icon-only button below `md`; header `sticky top-0 h-16 backdrop-blur-sm`, transparent background matching the page. No `AppControls`.
- Locale key: `layout.openSettings` "Cài đặt giao diện" / "Appearance settings" (aria-label for ⚙).

- [ ] **Step 1: Rewrite tests** (keep the existing breadcrumb/ellipsis assertions that still apply, stub `UserMenu`, `NotificationBell`):
  - `it('shows the deepest crumb as the page title and earlier crumbs as links')`.
  - `it('shows only the title on home')`.
  - `it('hides the menu toggle on desktop except in static mode')` → `it.each(['slim','slim-plus','drawer','horizontal'])`.
  - `it('toggles the static sidebar on desktop')` → `staticInactive` flips.
  - `it('opens the mobile drawer below lg in any mode')`.
  - `it('opens the palette and the settings panel')`.
  - `it('renders only the action cluster in the actions variant')`.
- [ ] **Step 2: Run — FAIL. Step 3: Implement. Step 4: Run — PASS. Step 5: Commit** `feat(frontend): redesign the app header`.

---

### Task 8: Vertical sidebar modes (static, slim, slim+), menu colors, mobile drawer, store swap

**Files:**

- Modify: `frontend/src/layouts/AppSidebar.vue`, `frontend/src/layouts/AppSidebarItem.vue`, `frontend/src/layouts/AppLayout.vue`
- Create: `frontend/src/layouts/sidebar-theme.ts` (class maps per `MenuTheme` × dark)
- Delete: `frontend/src/stores/sidebar-store.ts`, `frontend/tests/unit/stores/sidebar-store.test.ts`, `frontend/src/components/OrganizationSwitcher.vue`, `frontend/tests/components/OrganizationSwitcher.test.ts`
- Modify: `src/locales/{vi,en}.ts` (remove `nav.collapseSidebar`/`nav.expandSidebar` if unused)
- Test: `frontend/tests/layouts/{AppSidebar,AppSidebarItem,AppLayout}.test.ts`

**Interfaces:**

- Consumes: `useLayoutStore`, `useSidebarMenu`, `containsRoute` (Task 3), `useBrandWordmark`, `useThemePreference`, `AppHeader`, `AppearanceSettings`, `CommandPalette`, `UserMenu`.
- Produces: `sidebarThemeClasses(theme: MenuTheme, isDark: boolean): { surface: string; text: string; groupTitle: string; itemIdle: string; itemActive: string; divider: string }` in `sidebar-theme.ts` (values from Global Constraints); `AppSidebar` root `<aside data-testid="app-sidebar" :data-mode :data-theme>`; `AppLayout` root gets `data-testid="app-shell"` and mounts `<AppearanceSettings/>` and `<CommandPalette/>` once.
- Behavior (§3.1, §3.4–3.6, §4.2):
  - `static`: 16rem, group titles, accordion children, hidden entirely when `staticInactive`.
  - `slim` (4rem) / `slim-plus` (6rem, label under icon, `line-clamp-2`, `title`): one rail item per entry; leaf → tooltip + link; group or item with children → `n-dropdown`/popover to the right (`right-start`) with the group title as a header row and the active route highlighted.
  - Active parent: an entry/accordion whose `containsRoute(entry, route.name)` is true is highlighted and its accordion starts open.
  - Wordmark: white file when `menuTheme === 'brand' || isDark`, else `useBrandWordmark().wordmarkSrc`; mark image in rails.
  - Remove the account/org/logout footer (desktop and mobile) and the pale-rose `errorColor` override; keep the purple `Drawer` override only for `brand` + light.
  - Mobile (`< lg`): `n-drawer` left 280px, groups with titles, no footer; `watch(belowLg, v => { if (!v) layout.closeMobile() })`.
  - Main area: `drawer`/`horizontal` handled in Tasks 9–10; in this task `drawer` renders like `slim` and `horizontal` renders like `static` so every mode is usable.

- [ ] **Step 1: Rewrite tests:**
  - `AppSidebar`: `it.each(['static','slim','slim-plus'])('renders the %s layout')` (asserts `data-mode` and, for static, a group title text via `i18n.global.t`; for slim, no visible labels; for slim-plus, labels present); `it('hides the static sidebar when toggled off')`; `it('uses the theme-aware wordmark only for transparent light')`; `it('has no account or logout controls')`; `it('closes the mobile drawer when the window grows past lg')` (stub `matchMedia` with a fireable list).
  - `AppSidebarItem`: `it('opens the accordion of the parent that contains the current route')`; `it('opens a group flyout from the rail and marks the current page')`.
  - `AppLayout`: `it('mounts the settings panel and the palette once')`; keep existing scroll/notification tests.
- [ ] **Step 2: Run — FAIL. Step 3: Implement** (delete the old store/switcher files and their tests; `grep -rn "useSidebarStore\|OrganizationSwitcher" src tests` must return nothing). **Step 4: Run** `bun run --cwd frontend test:run tests/layouts tests/components` and `type-check` — PASS.
- [ ] **Step 5: Commit** `feat(frontend): rebuild the sidebar for static, slim and slim+ with menu colors`.

---

### Task 9: Drawer mode (hover expand, touch, pin)

**Files:**

- Modify: `frontend/src/layouts/AppSidebar.vue`, `frontend/src/layouts/AppLayout.vue`, `src/locales/{vi,en}.ts`
- Test: `frontend/tests/layouts/AppSidebar.test.ts`

**Interfaces:**

- Consumes: `useLayoutStore` (`drawerExpanded`, `expandDrawer`, `collapseDrawer`, `drawerPinned`, `setDrawerPinned`).
- Behavior (§3.1, §3.2): the rail (4rem) always reserves layout space; when expanded and not pinned, a full 16rem panel overlays content (`absolute`, shadow, `z-20`) — main content does not shift. Hover devices: `mouseenter` on the rail expands, `mouseleave` from the panel collapses. `(hover: none)`: tap on empty rail area or logo expands; a leaf tap navigates without expanding; tap outside or choosing an item collapses. Esc collapses when unpinned. Pin button (testid `app-sidebar-pin`, `aria-pressed`) in the expanded panel header; pinned → behaves as `static` (pushes content), persisted.
- Locale keys: `layout.pinSidebar` "Ghim thanh bên" / "Pin sidebar"; `layout.unpinSidebar` "Bỏ ghim thanh bên" / "Unpin sidebar".

- [ ] **Step 1: Failing tests:** `it('expands over the content on hover and collapses on leave')` (main element's left offset/class unchanged); `it('expands on tap on touch screens and navigates on a leaf tap without expanding')` (stub `(hover: none)`); `it('collapses on Escape when unpinned')`; `it('pins and stays pinned after a reload')` (new pinia → still pinned, `data-mode="drawer"` with full width).
- [ ] **Step 2: Run — FAIL. Step 3: Implement. Step 4: Run — PASS. Step 5: Commit** `feat(frontend): add the drawer menu mode`.

---

### Task 10: Horizontal mode

**Files:**

- Create: `frontend/src/layouts/AppTopMenu.vue`, `frontend/src/layouts/overflow-split.ts`
- Modify: `frontend/src/layouts/AppLayout.vue`, `src/locales/{vi,en}.ts`
- Test: `frontend/tests/unit/layouts/overflow-split.test.ts`, `frontend/tests/layouts/AppTopMenu.test.ts`, `frontend/tests/layouts/AppLayout.test.ts`

**Interfaces:**

- Produces: `splitOverflow(itemWidths: number[], available: number, moreWidth: number): number` — how many leading items stay visible; if all fit, `itemWidths.length`; otherwise the largest `n` with `sum(first n) + moreWidth <= available` (may be `0`).
- Produces: `AppTopMenu` (testid `app-top-menu`) — row 1 of §3.3 in the menu color: logo · entries (group/parent → `n-dropdown` downward with nested children; leaf → link) · "Thêm ▾" (testid `app-top-menu-more`) holding the overflowed entries (a group becomes a submenu) · `<AppHeader variant="actions"/>`. Measures with `ResizeObserver` on the entries container; active route/parent highlighted via `containsRoute`.
- `AppLayout` in horizontal (≥ `lg`): column layout — `AppTopMenu`, then `main` whose first child is a title + breadcrumb row (reuse the header's title/crumb rendering by extracting it into `layouts/PageTitle.vue` with the same testids), then `RouterView`; no `AppSidebar` at ≥ `lg`, mobile drawer unchanged below `lg`.
- Locale key: `layout.more` "Thêm" / "More".

- [ ] **Step 1: Failing unit tests** for `splitOverflow`: all fit → length; `[100,100,100]`, available 250, more 60 → `1`; available 50 → `0`; empty list → `0`.
- [ ] **Step 2: Implement; run — PASS.**
- [ ] **Step 3: Failing component tests:** `it('moves the entries that do not fit into More')` (stub `ResizeObserver` and element widths); `it('turns an overflowed group into a submenu of More')`; `it('highlights the entry containing the current route')`; `AppLayout`: `it('lays out the top menu and page title in horizontal mode on desktop')` (no `app-sidebar`, has `app-top-menu`, `app-header-title` inside `main`).
- [ ] **Step 4: Implement; run — PASS. Step 5: Commit** `feat(frontend): add the horizontal menu mode`.

---

### Task 11: E2E helper, guide capture tools, user guide content

**Files:**

- Modify: `frontend/e2e/fixtures.ts:19-26` (`logout`)
- Modify: `frontend/user-guide/tools/fixtures.ts:34` (`APP_SHELL`), `frontend/user-guide/tools/sections/base/{getting-started,account,notifications,organizations}.capture.ts`
- Modify: `frontend/user-guide/site/src/base/account/profile.md`, `frontend/user-guide/site/src/base/organizations/switch.md`, their `en/` counterparts, and every other page found by `grep -rniE "thanh bên|góc dưới|đăng xuất|sidebar|bottom of the|lower left" frontend/user-guide/site/src`
- Create (optional per spec §9.2): `base/account/appearance.md` (vi + en) via `bun run --cwd frontend/user-guide/site new-page base/account/appearance`

**Interfaces:**

- `logout(page)`: click `[data-testid="app-header-user-menu"]`, then `[data-testid="user-menu-logout"]`, then the existing confirm-dialog step.
- `APP_SHELL = '[data-testid="app-shell"]'`; capture scripts open the avatar menu (`app-header-user-menu`) instead of `app-sidebar-account`/`app-sidebar-logout`/`organization-switcher`, and use `organization-list-manage`.

- [ ] **Step 1: Update `logout()` and capture selectors.**
- [ ] **Step 2: Rewrite guide text** to the new locations (avatar menu top-right; ⚙ for appearance) in both languages, following `frontend/user-guide/README.md` authoring rules.
- [ ] **Step 3: Run** `bun run --cwd frontend/user-guide/site test` and `bun run --cwd frontend/user-guide/site build` — Expected: PASS, no dead links.
- [ ] **Step 4: Run** (needs a running backend + seeded data; report if unavailable rather than skipping silently) `bun run --cwd frontend guide:capture` and `bun run --cwd frontend test:e2e` — Expected: PASS; new screenshots written.
- [ ] **Step 5: Commit** `docs(guide): describe the new header and appearance settings` (guide files + images) and `test(e2e): log out through the user menu` (e2e + tools) as two commits.

---

### Task 12: Full gate and visual check

- [ ] **Step 1:** `bun run --cwd frontend format` then `git diff --stat` — only intended files changed.
- [ ] **Step 2:** `bun run --cwd frontend build-only && bun run --cwd frontend type-check && bun run --cwd frontend test:run` — Expected: all PASS.
- [ ] **Step 3:** `scripts/test-gate.sh` from the repo root — Expected: PASS.
- [ ] **Step 4:** Run the app (`bun run --cwd frontend dev` + backend) and walk all 20 combinations (5 modes × 2 colors × light/dark) at 1440px, plus 1024px touch emulation for drawer, plus 390px mobile; screenshot anything off and fix in the owning task's files.
- [ ] **Step 5:** Commit any formatting/fix-ups with an explicit file list.

---

## Self-review notes

- Spec coverage: §3.1–3.6 → Tasks 3, 8, 9, 10; §4 → Task 8; §5 → Tasks 6, 7, 10; §6 → Task 4; §7 → Tasks 1, 5; §8 → Task 2; §9.1 → each task + Task 11; §9.2 → Task 11; success criteria → Task 12.
- Names used across tasks: `useLayoutStore`, `MenuMode`, `MenuTheme`, `SidebarMenuEntry`, `flattenMenuLinks`, `containsRoute`, `registerPaletteAction`, `initialsOf`, `sidebarThemeClasses`, `splitOverflow`, testids `app-shell`, `app-sidebar`, `app-top-menu`, `app-header-user-menu`, `user-menu-logout`, `organization-list-manage`.
