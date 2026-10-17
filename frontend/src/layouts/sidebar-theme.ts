import type { MenuTheme } from '@/lib/layout-preferences'

export interface SidebarThemeClasses {
  /** Background of the sidebar (and of the horizontal bar). */
  surface: string
  /** Strong text on the surface: a parent that holds the current page. Must differ from the idle color. */
  text: string
  /** Small uppercase group titles. */
  groupTitle: string
  /** A menu item at rest, with its hover state. */
  itemIdle: string
  /** The menu item of the current page (and a rail entry holding it). */
  itemActive: string
  /** Border color for the sidebar's edges and the accordion guide line. */
  divider: string
}

// Full literal class strings, never assembled: Tailwind only generates classes it finds verbatim in source.
// `isDark` is resolved by the caller, so no `dark:` variants here.
const THEMES: Record<MenuTheme, Record<'light' | 'dark', SidebarThemeClasses>> = {
  brand: {
    light: {
      surface: 'bg-primary-600',
      text: 'text-white',
      groupTitle: 'text-white/50',
      itemIdle: 'text-white/70 hover:bg-white/10 hover:text-white',
      itemActive: 'bg-white/15 font-medium text-white',
      divider: 'border-white/10',
    },
    dark: {
      surface: 'bg-surface-900',
      text: 'text-surface-100',
      groupTitle: 'text-surface-500',
      itemIdle: 'text-surface-300 hover:bg-surface-700 hover:text-surface-100',
      itemActive: 'bg-primary-400/10 font-medium text-primary-200',
      divider: 'border-surface-800',
    },
  },
  transparent: {
    light: {
      surface: 'bg-surface-100',
      text: 'text-primary-600',
      groupTitle: 'text-surface-400',
      itemIdle: 'text-surface-600 hover:bg-surface-200 hover:text-surface-900',
      itemActive: 'bg-white font-medium text-primary-600 shadow-sm',
      divider: 'border-surface-200',
    },
    dark: {
      surface: 'bg-surface-950',
      text: 'text-primary-200',
      groupTitle: 'text-surface-500',
      itemIdle: 'text-surface-300 hover:bg-surface-900 hover:text-surface-100',
      itemActive: 'bg-surface-800 font-medium text-primary-200',
      divider: 'border-surface-800',
    },
  },
}

export function sidebarThemeClasses(theme: MenuTheme, isDark: boolean): SidebarThemeClasses {
  return THEMES[theme][isDark ? 'dark' : 'light']
}
