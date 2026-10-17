import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import i18n from '@/plugins/i18n'
import ProfileDialog from '@/components/ProfileDialog.vue'
import UserMenu from '@/layouts/UserMenu.vue'
import { setConfirmationOverrideNaive } from '@/lib/confirmation-override'
import { renderComponent } from '../helpers/render'
import { storeIn } from '../helpers/pinia'

const t = i18n.global.t

function render(
  account: Record<string, unknown>,
  organizationName: string | null = null,
  props: { tone?: 'default' | 'on-brand' } = {},
) {
  return renderComponent(UserMenu, {
    props,
    initialState: {
      auth: { account, organizationName },
      organizations: { loaded: true, myOrganizations: [] },
    },
    global: { stubs: { teleport: true, ProfileDialog: true } },
  })
}

async function openMenu(wrapper: Awaited<ReturnType<typeof render>>['wrapper']) {
  await wrapper.get('[data-testid="app-header-user-menu"]').trigger('click')
  await flushPromises()
}

describe('UserMenu', () => {
  afterEach(() => {
    // Back to the default from tests/setup.ts: accept immediately.
    setConfirmationOverrideNaive((options) => {
      void options.accept?.()
    })
  })

  it('shows initials, name and the current organization on the trigger', async () => {
    const { wrapper } = await render(
      { id: '1', name: 'Nguyễn Trọng', username: 'nt', email: 'nt@example.com' },
      'Acme',
    )

    const trigger = wrapper.get('[data-testid="app-header-user-menu"]')
    expect(trigger.text()).toContain('NT')
    expect(trigger.text()).toContain('Nguyễn Trọng')
    expect(trigger.text()).toContain('Acme')
  })

  it('draws the trigger in white tones on the brand menu bar', async () => {
    const account = { id: '1', name: 'Jane Doe', username: 'jane' }
    const brand = await render(account, 'Acme', { tone: 'on-brand' })

    const avatar = brand.wrapper.get('[data-testid="user-menu-avatar"]')
    expect(avatar.classes()).toContain('bg-white/20')
    expect(avatar.classes()).not.toContain('bg-primary-600')
    const org = brand.wrapper.get('[data-testid="user-menu-organization"]')
    expect(org.classes()).toContain('text-white/70')
    expect(org.classes()).not.toContain('text-surface-500')
    expect(brand.wrapper.get('[data-testid="user-menu-chevron"]').classes()).toContain(
      'text-white/70',
    )
    brand.wrapper.unmount()

    const plain = await render(account, 'Acme')
    expect(plain.wrapper.get('[data-testid="user-menu-avatar"]').classes()).toContain(
      'bg-primary-600',
    )
    expect(plain.wrapper.get('[data-testid="user-menu-organization"]').classes()).toContain(
      'text-surface-500',
    )
    expect(plain.wrapper.get('[data-testid="user-menu-chevron"]').classes()).toContain(
      'text-surface-400',
    )
  })

  it('shows the personal workspace when no organization is active', async () => {
    const { wrapper } = await render({ id: '1', name: 'Jane Doe', username: 'jane' })

    expect(wrapper.get('[data-testid="app-header-user-menu"]').text()).toContain(
      t('organizations.personalWorkspace'),
    )
  })

  it('falls back to the username, then to the generic user label', async () => {
    const { wrapper } = await render({ id: '1', name: '', username: 'jane' })
    expect(wrapper.get('[data-testid="app-header-user-menu"]').text()).toContain('jane')
    wrapper.unmount()

    const second = await render({ id: '1', name: '', username: '' })
    expect(second.wrapper.get('[data-testid="app-header-user-menu"]').text()).toContain(
      t('common.user'),
    )
  })

  it('shows the account email in the panel', async () => {
    const { wrapper } = await render({
      id: '1',
      name: 'Jane Doe',
      username: 'jane',
      email: 'jane@example.com',
    })

    await openMenu(wrapper)

    expect(wrapper.get('[role="menu"]').text()).toContain('jane@example.com')
  })

  it('asks for confirmation before logging out', async () => {
    const override = vi.fn()
    setConfirmationOverrideNaive(override)
    const { wrapper } = await render({ id: '1', name: 'Jane Doe', username: 'jane' })
    await openMenu(wrapper)

    await wrapper.get('[data-testid="user-menu-logout"]').trigger('click')

    expect(override).toHaveBeenCalledWith(expect.objectContaining({ header: t('auth.logout') }))
  })

  it('switches the locale from the menu', async () => {
    const { wrapper, pinia } = await render({ id: '1', name: 'Jane Doe', username: 'jane' })
    await openMenu(wrapper)

    await wrapper.get('[data-testid="user-menu-locale-vi"]').trigger('click')
    expect(storeIn(pinia, useLocaleStore).currentLocale).toBe('vi')

    await wrapper.get('[data-testid="user-menu-locale-en"]').trigger('click')
    expect(storeIn(pinia, useLocaleStore).currentLocale).toBe('en')
  })

  it('opens the profile dialog', async () => {
    const { wrapper, pinia } = await render({ id: '1', name: 'Jane Doe', username: 'jane' })
    await openMenu(wrapper)
    expect(wrapper.getComponent(ProfileDialog).props('visible')).toBe(false)

    await wrapper.get('[data-testid="user-menu-profile"]').trigger('click')

    expect(storeIn(pinia, useLayoutStore).profileOpen).toBe(true)
    expect(wrapper.getComponent(ProfileDialog).props('visible')).toBe(true)
  })
})
