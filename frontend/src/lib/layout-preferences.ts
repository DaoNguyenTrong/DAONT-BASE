export type MenuMode = 'static' | 'slim' | 'slim-plus' | 'drawer' | 'horizontal'
export type MenuTheme = 'brand' | 'transparent'

export const MENU_MODES: readonly MenuMode[] = [
  'static',
  'slim',
  'slim-plus',
  'drawer',
  'horizontal',
]
export const MENU_THEMES: readonly MenuTheme[] = ['brand', 'transparent']

export interface LayoutPreferences {
  menuMode: MenuMode
  menuTheme: MenuTheme
  drawerPinned: boolean
  staticInactive: boolean
}

export const DEFAULT_LAYOUT_PREFERENCES: LayoutPreferences = {
  menuMode: 'static',
  menuTheme: 'brand',
  drawerPinned: false,
  staticInactive: false,
}

export const LAYOUT_STORAGE_KEYS = {
  menuMode: 'layout-menu-mode',
  menuTheme: 'layout-menu-theme',
  drawerPinned: 'layout-drawer-pinned',
  staticInactive: 'layout-static-inactive',
} as const

export const LEGACY_SIDEBAR_MODE_KEY = 'sidebar-mode'

const LEGACY_MODE_MAP: Record<string, MenuMode> = { full: 'static', minimal: 'slim' }

function safeGet(key: string): string | null {
  try {
    return window.localStorage.getItem(key)
  } catch {
    return null
  }
}

function safeSet(key: string, value: string): void {
  try {
    window.localStorage.setItem(key, value)
  } catch {
    // Storage blocked or full: the preference simply isn't persisted.
  }
}

function safeRemove(key: string): void {
  try {
    window.localStorage.removeItem(key)
  } catch {
    // Nothing to clean up if storage is unavailable.
  }
}

function parseOneOf<T extends string>(raw: string | null, allowed: readonly T[], fallback: T): T {
  return allowed.includes(raw as T) ? (raw as T) : fallback
}

function parseBoolean(raw: string | null, fallback: boolean): boolean {
  if (raw === 'true') return true
  if (raw === 'false') return false
  return fallback
}

/** One-time move of the old `sidebar-mode` (full/minimal) into `layout-menu-mode`. */
function migrateLegacySidebarMode(): void {
  const legacy = safeGet(LEGACY_SIDEBAR_MODE_KEY)
  if (legacy === null) return
  if (safeGet(LAYOUT_STORAGE_KEYS.menuMode) === null) {
    const migrated = LEGACY_MODE_MAP[legacy]
    if (migrated) safeSet(LAYOUT_STORAGE_KEYS.menuMode, migrated)
  }
  safeRemove(LEGACY_SIDEBAR_MODE_KEY)
}

export function readLayoutPreferences(): LayoutPreferences {
  migrateLegacySidebarMode()
  const d = DEFAULT_LAYOUT_PREFERENCES
  return {
    menuMode: parseOneOf(safeGet(LAYOUT_STORAGE_KEYS.menuMode), MENU_MODES, d.menuMode),
    menuTheme: parseOneOf(safeGet(LAYOUT_STORAGE_KEYS.menuTheme), MENU_THEMES, d.menuTheme),
    drawerPinned: parseBoolean(safeGet(LAYOUT_STORAGE_KEYS.drawerPinned), d.drawerPinned),
    staticInactive: parseBoolean(safeGet(LAYOUT_STORAGE_KEYS.staticInactive), d.staticInactive),
  }
}

export function writeLayoutPreference<K extends keyof LayoutPreferences>(
  key: K,
  value: LayoutPreferences[K],
): void {
  safeSet(LAYOUT_STORAGE_KEYS[key], String(value))
}
