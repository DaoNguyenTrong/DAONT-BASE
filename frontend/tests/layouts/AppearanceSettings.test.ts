import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import i18n from '@/plugins/i18n'
import AppearanceSettings from '@/layouts/AppearanceSettings.vue'
import { DEFAULT_FONT_SIZE, setFontSize } from '@/lib/font-size'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'

const t = i18n.global.t

async function render() {
  const result = await renderComponent(AppearanceSettings, {
    initialState: { layout: { settingsOpen: true } },
    global: { stubs: { teleport: true } },
  })
  await flushPromises()
  return { ...result, layout: storeIn(result.pinia, useLayoutStore) }
}

function stubBelowLg(matches: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
  )
}

describe('AppearanceSettings', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    setFontSize(DEFAULT_FONT_SIZE)
  })

  it('applies and stores each menu mode', async () => {
    const { wrapper, layout } = await render()

    await wrapper.get('[data-testid="appearance-mode-slim-plus"]').trigger('click')

    expect(layout.menuMode).toBe('slim-plus')
    expect(localStorage.getItem('layout-menu-mode')).toBe('slim-plus')
    expect(
      wrapper.get('[data-testid="appearance-mode-slim-plus"]').attributes('aria-pressed'),
    ).toBe('true')
    expect(wrapper.get('[data-testid="appearance-mode-static"]').attributes('aria-pressed')).toBe(
      'false',
    )
  })

  it('changes the menu color', async () => {
    const { wrapper, layout } = await render()

    await wrapper.get('[data-testid="appearance-menu-theme-transparent"]').trigger('click')

    expect(layout.menuTheme).toBe('transparent')
    expect(localStorage.getItem('layout-menu-theme')).toBe('transparent')
    expect(
      wrapper
        .get('[data-testid="appearance-menu-theme-transparent"]')
        .attributes('aria-pressed'),
    ).toBe('true')
  })

  it('sets the system color mode', async () => {
    stubBelowLg(false)
    const { wrapper } = await render()

    await wrapper.get('[data-testid="appearance-theme-system"]').trigger('click')

    expect(localStorage.getItem('theme-preference')).toBe('system')
    expect(useThemePreference().preference.value).toBe('system')
    expect(wrapper.get('[data-testid="appearance-theme-system"]').attributes('aria-pressed')).toBe(
      'true',
    )
  })

  it('steps the font size within 14 to 20', async () => {
    setFontSize(19)
    const { wrapper } = await render()
    const increase = () => wrapper.get(`button[aria-label="${t('common.increaseFontSize')}"]`)

    await increase().trigger('click')

    expect(wrapper.text()).toContain('20px')
    expect(increase().attributes('disabled')).toBeDefined()
  })

  it('notes that the menu type applies on wide screens below lg', async () => {
    stubBelowLg(true)
    const { wrapper, layout } = await render()

    expect(wrapper.text()).toContain(t('layout.settings.menuModeWideOnly'))
    const noteId = wrapper
      .get('[data-testid="appearance-mode-slim"]')
      .attributes('aria-describedby')
    expect(noteId).toBeTruthy()
    expect(wrapper.get(`#${noteId}`).text()).toBe(t('layout.settings.menuModeWideOnly'))

    await wrapper.get('[data-testid="appearance-mode-slim"]').trigger('click')
    expect(layout.menuMode).toBe('slim')
  })

  it('omits the wide-screen note on desktop', async () => {
    stubBelowLg(false)
    const { wrapper } = await render()

    expect(wrapper.text()).not.toContain(t('layout.settings.menuModeWideOnly'))
    expect(
      wrapper.get('[data-testid="appearance-mode-slim"]').attributes('aria-describedby'),
    ).toBeUndefined()
  })
})
