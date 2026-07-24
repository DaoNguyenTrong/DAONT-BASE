import type { SupportedLocale } from '@/plugins/i18n'

export const useLocaleStore = defineStore('locale', () => {
  const { locale } = useI18n({ useScope: 'global' })

  const currentLocale = computed(() => locale.value as SupportedLocale)

  function setLocale(nextLocale: SupportedLocale) {
    if (currentLocale.value === nextLocale) {
      return
    }

    locale.value = nextLocale
    window.localStorage.setItem(LOCALE_STORAGE_KEY, nextLocale)
  }

  function toggleLocale() {
    setLocale(currentLocale.value === 'en' ? 'vi' : 'en')
  }

  return { currentLocale, setLocale, toggleLocale }
})
