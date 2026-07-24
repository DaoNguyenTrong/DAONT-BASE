import { describe, it, expect, beforeEach, afterEach } from 'vitest'
import AppControls from '@/components/AppControls.vue'
import { useThemePreference } from '@/composables/use-theme-preference'
import { DEFAULT_FONT_SIZE, MIN_FONT_SIZE, MAX_FONT_SIZE } from '@/lib/font-size'
import { renderComponent } from '../helpers/render'

async function openFontMenu(wrapper: Awaited<ReturnType<typeof renderComponent>>['wrapper']) {
  await wrapper.findAll('button')[1].trigger('click')
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
    useThemePreference().setPreference('light')
    document.documentElement.style.fontSize = ''
  })

  it('disables the increase button at the max font size', async () => {
    document.documentElement.style.fontSize = `${MAX_FONT_SIZE}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    expect(fontMenuButton(wrapper, 'Increase font size')?.attributes('disabled')).toBeDefined()
    expect(fontMenuButton(wrapper, 'Decrease font size')?.attributes('disabled')).toBeUndefined()
  })

  it('disables the decrease button at the min font size', async () => {
    document.documentElement.style.fontSize = `${MIN_FONT_SIZE}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    expect(fontMenuButton(wrapper, 'Decrease font size')?.attributes('disabled')).toBeDefined()
    expect(fontMenuButton(wrapper, 'Increase font size')?.attributes('disabled')).toBeUndefined()
  })

  it('disables the reset button at the default font size', async () => {
    document.documentElement.style.fontSize = `${DEFAULT_FONT_SIZE}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    expect(fontMenuButton(wrapper, 'Reset font size')?.attributes('disabled')).toBeDefined()
  })

  it('increases the font size and updates the DOM style on click', async () => {
    document.documentElement.style.fontSize = `${DEFAULT_FONT_SIZE}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    await fontMenuButton(wrapper, 'Increase font size')?.trigger('click')

    expect(document.documentElement.style.fontSize).toBe(`${DEFAULT_FONT_SIZE + 1}px`)
  })

  it('decreases the font size and updates the DOM style on click', async () => {
    document.documentElement.style.fontSize = `${DEFAULT_FONT_SIZE}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    await fontMenuButton(wrapper, 'Decrease font size')?.trigger('click')

    expect(document.documentElement.style.fontSize).toBe(`${DEFAULT_FONT_SIZE - 1}px`)
  })

  it('resets the font size to default on click', async () => {
    document.documentElement.style.fontSize = `${MAX_FONT_SIZE}px`
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })
    await openFontMenu(wrapper)

    await fontMenuButton(wrapper, 'Reset font size')?.trigger('click')

    expect(document.documentElement.style.fontSize).toBe(`${DEFAULT_FONT_SIZE}px`)
  })

  it('toggles the theme icon between sun and moon on click', async () => {
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })

    // Light mode renders the sun icon (a <circle>), no <circle> in dark mode.
    expect(wrapper.find('circle').exists()).toBe(true)

    await wrapper.findAll('button')[0].trigger('click')

    expect(wrapper.find('circle').exists()).toBe(false)
  })

  it('toggles the locale on click, updating the language button label', async () => {
    const { wrapper } = await renderComponent(AppControls, {
      global: { stubs: { teleport: true } },
    })

    const localeButton = () => wrapper.findAll('button')[2]
    expect(localeButton().attributes('aria-label')).toContain('VI')

    await localeButton().trigger('click')

    expect(localeButton().attributes('aria-label')).toContain('EN')
  })
})
