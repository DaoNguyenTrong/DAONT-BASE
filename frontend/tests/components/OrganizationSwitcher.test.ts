import { afterEach, describe, expect, it, vi } from 'vitest'
import { NPopover } from 'naive-ui'
import OrganizationSwitcher from '@/components/OrganizationSwitcher.vue'
import { renderComponent } from '../helpers/render'

function stubBelowLg(matches: boolean) {
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => ({ matches, addEventListener: vi.fn(), removeEventListener: vi.fn() })),
  )
}

describe('OrganizationSwitcher', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  // Below `lg` the switcher sits at the foot of the 280px mobile drawer: a 240px menu opening to the
  // right would run off the screen.
  it('opens upward inside the mobile drawer', async () => {
    stubBelowLg(true)
    const { wrapper } = await renderComponent(OrganizationSwitcher)

    expect(wrapper.findComponent(NPopover).props('placement')).toBe('top-start')
  })

  // On desktop the switcher sits at the foot of the sidebar too: a menu hanging down from it would run
  // off the bottom of the screen and hide its last item (Manage organizations), so it grows upward.
  it('opens beside the desktop sidebar, growing upward', async () => {
    stubBelowLg(false)
    const { wrapper } = await renderComponent(OrganizationSwitcher)

    expect(wrapper.findComponent(NPopover).props('placement')).toBe('right-end')
  })
})
