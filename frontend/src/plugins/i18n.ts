import { createI18n } from 'vue-i18n'
import en, { type LocaleSchema } from '@/locales/en'
import vi from '@/locales/vi'

export const SUPPORTED_LOCALES = ['en', 'vi'] as const
export type SupportedLocale = (typeof SUPPORTED_LOCALES)[number]
export const LOCALE_STORAGE_KEY = 'app-locale'

function isSupportedLocale(value: string | null): value is SupportedLocale {
  return value === 'en' || value === 'vi'
}

const messages = {
  en,
  vi,
} satisfies Record<SupportedLocale, LocaleSchema>

const savedLocale =
  typeof window === 'undefined' ? null : window.localStorage.getItem(LOCALE_STORAGE_KEY)

export const defaultLocale: SupportedLocale = isSupportedLocale(savedLocale) ? savedLocale : 'vi'

const i18n = createI18n<[LocaleSchema], SupportedLocale>({
  legacy: false,
  locale: defaultLocale,
  fallbackLocale: 'en',
  messages,
})

export default i18n
