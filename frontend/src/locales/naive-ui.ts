import { viVN, enUS, dateViVN, dateEnUS } from 'naive-ui'
import type { SupportedLocale } from '@/plugins/i18n'

export default {
  en: { locale: enUS, dateLocale: dateEnUS },
  vi: { locale: viVN, dateLocale: dateViVN },
} satisfies Record<SupportedLocale, { locale: typeof enUS; dateLocale: typeof dateEnUS }>
