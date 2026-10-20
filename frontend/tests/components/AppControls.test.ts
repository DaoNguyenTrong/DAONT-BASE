import { describe, it, expect, beforeEach, afterEach, vi } from 'vitest'
import AppControls from '@/components/AppControls.vue'
import { useThemePreference } from '@/composables/use-theme-preference'
import { DEFAULT_FONT_SIZE, MIN_FONT_SIZE, MAX_FONT_SIZE } from '@/lib/font-size'
import { renderComponent } from '../helpers/render'
import en from '@/locales/en'

async function openFontMenu(wrapper: Awaited<ReturnType<typeof renderComponent>>['wrapper']) {
  await wrapper.findAll('button')[1]!.trigger('click')
}

function fontMenuButton(
  wrapper: Awaited<ReturnType<typeof renderComponent>>['wrapper'],
  label: string,
) {
  return wrapper.findAll('button').find((b) => b.text().includes(label))
}

describe('AppControls', () => {
  beforeEach(() => {
    document.documentElement.style.fontSize = ''
    document.documentElement.classList.remove('dark')
  })

  afterEach(() => {
    // useThemePreference()'s `isDark` ref is a module-level singleton — reset
    // it so state doesn't leak into later test files.
    vi.unstubAllGlobals()
    useThemePreference().setPreference('light')
    document.documentElement.style.fontSize = ''
  })

  it.each([
    [
      'disables the increase button at the max font size',
      MAX_FONT_SIZE,
      en.common.increaseFontSize,
      en.common.decreaseFontSize,
    ],
    [
      'disables the decrease button at the min font size',
      MIN_FONT_SIZE,
      en.common.decreaseFontSize,
      en.common.increaseFontSize,
    ],
    [
      'disables the reset button at the default font size',
      DEFAULT_FONT_SIZE,
      en.common.resetFontSize,
      null,
    ],
  ])('%s', async (_title, fontSize, disabledLabel, enabledLabel) => {
    document.documentElement.style.fontSize = `${fontSize}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    expect(fontMenuButton(wrapper, disabledLabel)?.attributes('disabled')).toBeDefined()
    if (enabledLabel !== null) {
      expect(fontMenuButton(wrapper, enabledLabel)?.attributes('disabled')).toBeUndefined()
    }
  })

  it.each([
    [
      'increases the font size and updates the DOM style on click',
      DEFAULT_FONT_SIZE,
      en.common.increaseFontSize,
      DEFAULT_FONT_SIZE + 1,
    ],
    [
      'decreases the font size and updates the DOM style on click',
      DEFAULT_FONT_SIZE,
      en.common.decreaseFontSize,
      DEFAULT_FONT_SIZE - 1,
    ],
    [
      'resets the font size to default on click',
      MAX_FONT_SIZE,
      en.common.resetFontSize,
      DEFAULT_FONT_SIZE,
    ],
  ])('%s', async (_title, fontSize, label, expected) => {
    document.documentElement.style.fontSize = `${fontSize}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    await fontMenuButton(wrapper, label)?.trigger('click')

    expect(document.documentElement.style.fontSize).toBe(`${expected}px`)
  })

  it('toggles the theme icon between sun and moon on click', async () => {
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    const themeButton = () => wrapper.findAll('button')[0]!

    // Light mode renders the sun icon (a <circle>), no <circle> in dark mode.
    expect(themeButton().find('circle').exists()).toBe(true)

    await themeButton().trigger('click')

    expect(themeButton().find('circle').exists()).toBe(false)
  })

  it('switches to the opposite of the shown theme when the setting is system', async () => {
    vi.stubGlobal('matchMedia', () => ({
      matches: true,
      addEventListener: () => {},
      removeEventListener: () => {},
    }))
    useThemePreference().setPreference('system')
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })

    await wrapper.findAll('button')[0]!.trigger('click')

    expect(localStorage.getItem('theme-preference')).toBe('light')
  })

  it('toggles the locale on click, updating the language button label', async () => {
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })

    const localeButton = () => wrapper.findAll('button')[2]!
    expect(localeButton().attributes('aria-label')).toContain('VI')

    await localeButton().trigger('click')

    expect(localeButton().attributes('aria-label')).toContain('EN')
  })
})
