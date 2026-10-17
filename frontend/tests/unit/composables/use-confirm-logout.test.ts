import { defineComponent } from 'vue'
import { afterEach, describe, expect, it, vi } from 'vitest'
import { flushPromises } from '@vue/test-utils'
import i18n from '@/plugins/i18n'
import { setConfirmationOverrideNaive } from '@/lib/confirmation-override'
import { useConfirmLogout } from '@/composables/use-confirm-logout'
import { renderComponent } from '../../helpers/render'
import { storeIn } from '../../helpers/pinia'

async function setup() {
  let confirmLogout!: () => void
  const Host = defineComponent({
    setup() {
      confirmLogout = useConfirmLogout()
      return () => null
    },
  })
  const { pinia } = await renderComponent(Host)
  return { confirmLogout, auth: storeIn(pinia, useAuthStore) }
}

describe('useConfirmLogout', () => {
  afterEach(() => {
    // Back to the default from tests/setup.ts: accept immediately.
    setConfirmationOverrideNaive((options) => {
      void options.accept?.()
    })
  })

  it('asks for confirmation with the logout copy before doing anything', async () => {
    const override = vi.fn()
    setConfirmationOverrideNaive(override)
    const { confirmLogout, auth } = await setup()
    const logout = vi.spyOn(auth, 'logout').mockResolvedValue(undefined)

    confirmLogout()

    expect(override).toHaveBeenCalledWith(
      expect.objectContaining({
        header: i18n.global.t('auth.logout'),
        message: i18n.global.t('auth.logoutConfirmMessage'),
        acceptLabel: i18n.global.t('common.confirm'),
        rejectLabel: i18n.global.t('common.cancel'),
      }),
    )
    expect(logout).not.toHaveBeenCalled()
  })

  it('logs out once the confirmation is accepted', async () => {
    const { confirmLogout, auth } = await setup()
    const logout = vi.spyOn(auth, 'logout').mockResolvedValue(undefined)

    confirmLogout()
    await flushPromises()

    expect(logout).toHaveBeenCalledOnce()
  })
})
