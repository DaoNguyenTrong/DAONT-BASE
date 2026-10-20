import { createMemoryHistory, createRouter } from 'vue-router'
import { createTestingPinia } from '@pinia/testing'
import { flushPromises, mount, type DOMWrapper, type VueWrapper } from '@vue/test-utils'
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { Building, Home, User } from '@vicons/tabler'
import { NDropdown, type DropdownOption } from 'naive-ui'
import AppTopMenu from '@/layouts/AppTopMenu.vue'
import type { MenuTheme } from '@/lib/layout-preferences'
import i18n from '@/plugins/i18n'

// Base ships no menu groups; one is needed to see a group entry and its submenu.
vi.mock('@/layouts/menu-groups', () => ({
  MENU_GROUPS: [
    { id: 'workspace', labelKey: 'layout.palette.pages', icon: { template: '<svg />' }, order: 2 },
  ],
}))

const t = i18n.global.t
const stub = { template: '<div />' }
const groupLabel = () => t('layout.palette.pages')

// jsdom-like environments have no layout: every entry is 100px wide, "More" 60px, and the room the bar
// leaves for its entries is whatever the test sets.
const ITEM_WIDTH = 100
const MORE_WIDTH = 60
let available = 1000

// The bar re-measures when its ResizeObserver fires; the test fires it by hand.
const observers: { callback: ResizeObserverCallback; targets: Element[] }[] = []
class ResizeObserverStub {
  private entry: { callback: ResizeObserverCallback; targets: Element[] }
  constructor(callback: ResizeObserverCallback) {
    this.entry = { callback, targets: [] }
    observers.push(this.entry)
  }
  observe(target: Element) {
    this.entry.targets.push(target)
  }
  unobserve() {}
  disconnect() {
    this.entry.targets = []
  }
}

async function resizeTo(wrapper: VueWrapper, width: number) {
  available = width
  const entries = entriesOf(wrapper).element
  observers
    .filter((observer) => observer.targets.includes(entries))
    .forEach((observer) => observer.callback([], observer as unknown as ResizeObserver))
  await flushPromises()
}

async function mountTopMenu(options: { path?: string; menuTheme?: MenuTheme } = {}) {
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
              sidebar: { labelKey: 'nav.organizations', icon: Building, group: 'workspace' },
            },
          },
          {
            path: 'profile',
            name: 'profile',
            component: stub,
            meta: { sidebar: { labelKey: 'nav.profile', icon: User, order: 3 } },
          },
        ],
      },
    ],
  })
  await router.push(options.path ?? '/')
  await router.isReady()

  const pinia = createTestingPinia({
    createSpy: vi.fn,
    stubActions: false,
    initialState: { layout: { menuMode: 'horizontal', menuTheme: options.menuTheme ?? 'brand' } },
  })

  const wrapper = mount(AppTopMenu, {
    global: {
      plugins: [pinia, router],
      stubs: {
        teleport: true,
        AppHeader: {
          props: ['variant', 'tone'],
          template: '<div data-testid="header-stub" :data-variant="variant" :data-tone="tone" />',
        },
      },
    },
  })
  await flushPromises()

  return { wrapper, router }
}

function entriesOf(wrapper: VueWrapper) {
  return wrapper.get('[data-testid="app-top-menu-entries"]')
}

async function openMore(wrapper: VueWrapper) {
  await wrapper.get('[data-testid="app-top-menu-more"]').trigger('click')
  await flushPromises()
  return wrapper.get('.n-dropdown-menu')
}

function optionWith(scope: Pick<DOMWrapper<Element>, 'findAll'>, text: string) {
  return scope.findAll('.n-dropdown-option-body').find((option) => option.text().includes(text))!
}

describe('AppTopMenu', () => {
  beforeEach(() => {
    available = 1000
    observers.length = 0
    vi.stubGlobal('ResizeObserver', ResizeObserverStub)
    vi.spyOn(Element.prototype, 'getBoundingClientRect').mockImplementation(function (
      this: Element,
    ) {
      const width = this.matches('[data-testid="app-top-menu-entries"]')
        ? available
        : this.hasAttribute('data-measure-more')
          ? MORE_WIDTH
          : this.hasAttribute('data-measure-item')
            ? ITEM_WIDTH
            : 0
      return DOMRect.fromRect({ width, height: 0 })
    })
  })

  afterEach(() => {
    vi.unstubAllGlobals()
    vi.restoreAllMocks()
    useThemePreference().setPreference('light')
  })

  it('shows every top-level entry and the header actions when they fit', async () => {
    const { wrapper } = await mountTopMenu()

    const entries = entriesOf(wrapper)
    expect(entries.text()).toContain(t('nav.home'))
    expect(entries.text()).toContain(groupLabel())
    expect(entries.text()).toContain(t('nav.profile'))
    // A group's own items live in its dropdown, not on the bar.
    expect(entries.text()).not.toContain(t('nav.organizations'))
    expect(wrapper.find('[data-testid="app-top-menu-more"]').exists()).toBe(false)
    expect(wrapper.get('[data-testid="header-stub"]').attributes('data-variant')).toBe('actions')
  })

  it('moves the entries that do not fit into More', async () => {
    const { wrapper } = await mountTopMenu()

    // 250px holds one 100px entry beside the 60px More.
    await resizeTo(wrapper, 250)

    const entries = entriesOf(wrapper)
    expect(entries.text()).toContain(t('nav.home'))
    expect(entries.text()).not.toContain(groupLabel())
    expect(entries.text()).not.toContain(t('nav.profile'))
    const more = wrapper.get('[data-testid="app-top-menu-more"]')
    expect(more.text()).toContain(t('layout.more'))

    const menu = await openMore(wrapper)
    expect(menu.text()).toContain(groupLabel())
    expect(menu.text()).toContain(t('nav.profile'))
    expect(menu.text()).not.toContain(t('nav.home'))

    // Room again: everything is back on the bar and More is gone.
    await resizeTo(wrapper, 1000)
    expect(entriesOf(wrapper).text()).toContain(t('nav.profile'))
    expect(wrapper.find('[data-testid="app-top-menu-more"]').exists()).toBe(false)
  })

  it('turns an overflowed group into a submenu of More', async () => {
    const { wrapper } = await mountTopMenu()
    await resizeTo(wrapper, 250)

    const menu = await openMore(wrapper)
    // The group is one option of More; its items wait in a submenu, not in a section of More.
    expect(menu.find('.n-dropdown-option-body--group').exists()).toBe(false)
    expect(menu.text()).not.toContain(t('nav.organizations'))

    // The submenu opens on hover, which needs real layout; its content is checked on the options instead.
    // At this width More is the bar's only dropdown.
    const options = wrapper.getComponent(NDropdown).props('options') as DropdownOption[]
    const group = options.find((option) => option.label === groupLabel())!
    expect(group.type).toBeUndefined()
    expect((group.children as DropdownOption[]).map((child) => child.key)).toEqual([
      'organizations',
    ])
  })

  it('navigates to the page picked in More', async () => {
    const { wrapper, router } = await mountTopMenu()
    await resizeTo(wrapper, 250)

    const menu = await openMore(wrapper)
    await optionWith(menu, t('nav.profile')).trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('profile')
  })

  it('highlights the entry containing the current route', async () => {
    const { wrapper } = await mountTopMenu({ path: '/organizations' })

    const entries = entriesOf(wrapper)
    const group = entries.get('button[aria-haspopup="menu"]')
    expect(group.text()).toContain(groupLabel())
    expect(group.classes()).toContain('bg-white/15')
    const home = entries.findAll('a').find((link) => link.text().includes(t('nav.home')))!
    expect(home.classes()).not.toContain('bg-white/15')

    // The group's dropdown marks the current page too.
    await group.trigger('click')
    await flushPromises()
    const active = wrapper.get('.n-dropdown-menu .n-dropdown-option-body--active')
    expect(active.text()).toContain(t('nav.organizations'))
  })

  it('highlights More when it holds the current route', async () => {
    const { wrapper } = await mountTopMenu({ path: '/profile' })
    await resizeTo(wrapper, 250)

    expect(wrapper.get('[data-testid="app-top-menu-more"]').classes()).toContain('bg-white/15')
  })

  it('paints the bar with the menu color and draws a bottom border on the transparent one', async () => {
    const brand = await mountTopMenu()
    const brandBar = brand.wrapper.get('[data-testid="app-top-menu"]')
    expect(brandBar.classes()).toContain('bg-primary-600')
    expect(brandBar.classes()).not.toContain('border-b')
    brand.wrapper.unmount()

    const transparent = await mountTopMenu({ menuTheme: 'transparent' })
    const transparentBar = transparent.wrapper.get('[data-testid="app-top-menu"]')
    expect(transparentBar.classes()).toContain('bg-surface-100')
    expect(transparentBar.classes()).toContain('border-b')
  })

  it('asks the header for its on-brand look on the light brand bar only', async () => {
    const toneOf = (wrapper: VueWrapper) =>
      wrapper.get('[data-testid="header-stub"]').attributes('data-tone')

    const brand = await mountTopMenu()
    expect(toneOf(brand.wrapper)).toBe('on-brand')
    brand.wrapper.unmount()

    const transparent = await mountTopMenu({ menuTheme: 'transparent' })
    expect(toneOf(transparent.wrapper)).toBe('default')
    transparent.wrapper.unmount()

    // The dark brand bar is a dark surface, which the header's default look already suits.
    useThemePreference().setPreference('dark')
    const brandDark = await mountTopMenu()
    expect(toneOf(brandDark.wrapper)).toBe('default')
  })

  it('goes home from the logo', async () => {
    const { wrapper, router } = await mountTopMenu({ path: '/profile' })

    await wrapper.get('[data-testid="app-top-menu-home"]').trigger('click')
    await flushPromises()

    expect(router.currentRoute.value.name).toBe('home')
  })
})
