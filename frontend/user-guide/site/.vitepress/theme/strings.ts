import type { Locale } from '../sidebar-types'

// UI text the theme renders itself (VitePress's own labels live in config.mts).
export const strings: Record<
  Locale,
  { guide: string; openApp: string; requiresPermission: string; versionTitle: string }
> = {
  vi: {
    guide: 'Hướng dẫn sử dụng',
    openApp: 'Mở ứng dụng',
    requiresPermission: 'Cần quyền',
    versionTitle: 'Áp dụng cho phiên bản',
  },
  en: {
    guide: 'User guide',
    openApp: 'Open app',
    requiresPermission: 'Requires permission',
    versionTitle: 'Applies to version',
  },
}
