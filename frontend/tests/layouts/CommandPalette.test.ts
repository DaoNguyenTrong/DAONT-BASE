import { afterEach, describe, expect, it, vi } from 'vitest'
import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount } from '@vue/test-utils'
import { Building, Home, Settings } from '@vicons/tabler'
import CommandPalette from '@/layouts/CommandPalette.vue'
import { registerPaletteAction, usePaletteActions } from '@/lib/command-palette'
import { useAuthStore } from '@/stores/auth'
import { useLayoutStore } from '@/stores/layout-store'
import { setConfirmationOverrideNaive } from '@/lib/confirmation-override'
import i18n from '@/plugins/i18n'
import { storeIn } from '../helpers/pinia'

const t = i18n.global.t
const stub = { template: '<div />' }

// Every wrapper is unmounted here, before the afterEach below empties <body>: the palette listens on
// `window` and must not outlive its test.
const mounted: { unmount: () => void }[] = []

async function render() {
  const router = createRouter({
    history: createMemoryHistory(),
    routes: [
      {
        path: '/',
        name: 'layout',
        component: stub,
        children: [
          {
            path: '',
            name: 'home',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.home', icon: Home, order: 1 } },
          },
          {
            path: 'organizations',
            name: 'organizations',
            component: stub,
            meta: {
              permission: 'organizations.view',
              sidebar: { labelKey: 'nav.organizations', icon: Building, order: 2 },
            },
          },
        ],
      },
    ],
  })
  await router.push('/')
  await router.isReady()

  const pinia = createTestingPinia({
    createSpy: vi.fn,
    stubActions: false,
    initialState: { auth: { permissions: ['organizations.view'] } },
  })
  const wrapper = mount(CommandPalette, {
    attachTo: document.body,
    global: { plugins: [pinia, router], stubs: { teleport: true } },
  })
  mounted.push(wrapper)

  return { wrapper, router, layout: storeIn(pinia, useLayoutStore), auth: storeIn(pinia, useAuthStore) }
}

function pressK(modifiers: KeyboardEventInit, target: EventTarget = window) {
  const event = new KeyboardEvent('keydown', {
    key: 'k',
    ...modifiers,
    bubbles: true,
    cancelable: true,
  })
  target.dispatchEvent(event)
  return event
}

async function press(wrapper: Awaited<ReturnType<typeof render>>['wrapper'], key: string) {
  await wrapper.get('input').trigger('keydown', { key })
  await flushPromises()
}

describe('CommandPalette', () => {
  afterEach(() => {
    mounted.splice(0).forEach((w) => w.unmount())
    document.body.innerHTML = ''
    // Back to the default from tests/setup.ts: accept immediately.
    setConfirmationOverrideNaive((options) => {
      void options.accept?.()
    })
  })

  async function open() {
    const result = await render()
    result.layout.openPalette()
    await flushPromises()
    return result
  }

  it('opens on Ctrl K and on Meta K', async () => {
    const { layout } = await render()

    expect(pressK({ ctrlKey: true }).defaultPrevented).toBe(true)
    expect(layout.paletteOpen).toBe(true)

    layout.closePalette()
    pressK({ metaKey: true })
    expect(layout.paletteOpen).toBe(true)
  })

  it('leaves Ctrl Shift K and Ctrl Alt K to the browser', async () => {
    const { layout } = await render()

    expect(pressK({ ctrlKey: true, shiftKey: true }).defaultPrevented).toBe(false)
    expect(pressK({ ctrlKey: true, altKey: true }).defaultPrevented).toBe(false)
    expect(layout.paletteOpen).toBe(false)
  })

  it('ignores a held-down Ctrl K', async () => {
    const { layout } = await render()

    expect(pressK({ ctrlKey: true, repeat: true }).defaultPrevented).toBe(false)
    expect(layout.paletteOpen).toBe(false)
  })

  it('leaves Ctrl K to the browser inside an element marked data-command-palette-ignore', async () => {
    const { layout } = await render()
    const editor = document.body.appendChild(document.createElement('div'))
    editor.setAttribute('data-command-palette-ignore', '')
    const textarea = editor.appendChild(document.createElement('textarea'))

    expect(pressK({ ctrlKey: true }, textarea).defaultPrevented).toBe(false)
    expect(layout.paletteOpen).toBe(false)

    // Anywhere else it still opens.
    expect(pressK({ ctrlKey: true }, document.body).defaultPrevented).toBe(true)
    expect(layout.paletteOpen).toBe(true)
  })

  it('does not stack a second dialog when Ctrl K is pressed while open', async () => {
    const { wrapper } = await open()

    pressK({ ctrlKey: true })
    await flushPromises()

    expect(wrapper.findAll('.n-modal')).toHaveLength(1)
    expect(wrapper.findAll('input')).toHaveLength(1)
  })

  it('stops listening once unmounted', async () => {
    const { wrapper, layout } = await render()

    wrapper.unmount()
    pressK({ ctrlKey: true })

    expect(layout.paletteOpen).toBe(false)
  })

  it('lists the pages the user can reach with the localized placeholder', async () => {
    const { wrapper } = await open()

    expect(wrapper.get('input').attributes('placeholder')).toBe(t('layout.palette.placeholder'))
    expect(wrapper.text()).toContain(t('nav.home'))
    expect(wrapper.text()).toContain(t('nav.organizations'))
  })

  it('shows the empty state when nothing matches', async () => {
    const { wrapper } = await open()

    await wrapper.get('input').setValue('zzzzqqq')

    expect(wrapper.text()).toContain(t('layout.palette.empty'))
  })

  it('never offers a page the user lacks permission for, even after permissions change while open', async () => {
    const { wrapper, auth } = await open()
    expect(wrapper.text()).toContain(t('nav.organizations'))

    auth.permissions = []
    await flushPromises()

    expect(wrapper.text()).not.toContain(t('nav.organizations'))
    expect(wrapper.text()).toContain(t('nav.home'))
  })

  it('moves the highlighted option with the arrow keys', async () => {
    const { wrapper } = await open()
    const input = wrapper.get('input')
    const first = input.attributes('aria-activedescendant')

    await press(wrapper, 'ArrowDown')

    const second = input.attributes('aria-activedescendant')
    expect(second).not.toBe(first)
    expect(wrapper.get(`#${second}`).attributes('aria-selected')).toBe('true')

    await press(wrapper, 'ArrowUp')
    expect(input.attributes('aria-activedescendant')).toBe(first)
  })

  it('navigates to the highlighted page on Enter and closes', async () => {
    const { wrapper, router, layout } = await open()

    await press(wrapper, 'ArrowDown')
    await press(wrapper, 'Enter')

    expect(router.currentRoute.value.name).toBe('organizations')
    expect(layout.paletteOpen).toBe(false)
  })

  it('closes on Escape', async () => {
    const { wrapper, layout } = await open()

    await press(wrapper, 'Escape')

    expect(layout.paletteOpen).toBe(false)
  })

  it('runs a fork-registered action', async () => {
    const spy = vi.fn()
    const unregister = registerPaletteAction({
      id: 'x',
      labelKey: 'nav.profile',
      icon: Settings,
      keywords: ['zebra'],
      run: spy,
    })
    const { wrapper } = await open()

    await wrapper.get('input').setValue('zebra')
    await press(wrapper, 'Enter')
    unregister()

    expect(spy).toHaveBeenCalledOnce()
  })

  it('opens the appearance settings from the base action', async () => {
    const { wrapper, layout } = await open()

    await wrapper.get('input').setValue(t('layout.settings.title'))
    await press(wrapper, 'Enter')

    expect(layout.settingsOpen).toBe(true)
    expect(layout.paletteOpen).toBe(false)
  })

  it('removes its base actions from the registry once unmounted', async () => {
    const { wrapper } = await render()
    expect(usePaletteActions().value.some((a) => a.id === 'base.logout')).toBe(true)

    wrapper.unmount()

    expect(usePaletteActions().value.some((a) => a.id === 'base.logout')).toBe(false)
  })

  it('asks for confirmation before logging out from the palette', async () => {
    const override = vi.fn()
    setConfirmationOverrideNaive(override)
    const { wrapper } = await open()

    await wrapper.get('input').setValue(t('auth.logout'))
    await press(wrapper, 'Enter')

    expect(override).toHaveBeenCalledOnce()
    expect(override.mock.calls[0]![0].header).toBe(t('auth.logout'))
  })
})
