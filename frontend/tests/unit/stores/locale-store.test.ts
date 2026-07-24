import { mount } from '@vue/test-utils'
import { createTestingPinia } from '@pinia/testing'
import { describe, expect, it, vi } from 'vitest'
import { useLocaleStore } from '@/stores/locale-store'

function mountLocaleStore() {
  let store!: ReturnType<typeof useLocaleStore>
  const wrapper = mount(
    {
      template: '<div />',
      setup() {
        store = useLocaleStore()
        return {}
      },
    },
    {
      global: {
        plugins: [createTestingPinia({ createSpy: vi.fn, stubActions: false })],
      },
    },
  )
  return { store, wrapper }
}

describe('useLocaleStore', () => {
  it('starts with the current i18n locale (en, reset by global test setup)', () => {
    const { store } = mountLocaleStore()

    expect(store.currentLocale).toBe('en')
  })

  it('setLocale switches the locale and persists it to localStorage', () => {
    const { store } = mountLocaleStore()

    store.setLocale('vi')

    expect(store.currentLocale).toBe('vi')
    expect(window.localStorage.getItem('app-locale')).toBe('vi')
  })

  it('setLocale is a no-op when already on the target locale', () => {
    const { store } = mountLocaleStore()

    expect(window.localStorage.getItem('app-locale')).toBeNull()

    store.setLocale('en')

    expect(store.currentLocale).toBe('en')
    expect(window.localStorage.getItem('app-locale')).toBeNull()
  })

  it('toggleLocale flips between en and vi', () => {
    const { store } = mountLocaleStore()

    store.toggleLocale()
    expect(store.currentLocale).toBe('vi')
    expect(window.localStorage.getItem('app-locale')).toBe('vi')

    store.toggleLocale()
    expect(store.currentLocale).toBe('en')
    expect(window.localStorage.getItem('app-locale')).toBe('en')
  })
})
