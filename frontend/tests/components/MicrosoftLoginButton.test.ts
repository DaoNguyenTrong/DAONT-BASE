import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import MicrosoftLoginButton from '@/components/MicrosoftLoginButton.vue'
import { renderComponent } from '../helpers/render'

const initializeMock = vi.fn()
const loginPopupMock = vi.fn()

vi.mock('@azure/msal-browser', () => ({
  PublicClientApplication: vi.fn().mockImplementation(function (this: object) {
    Object.assign(this, { initialize: initializeMock, loginPopup: loginPopupMock })
  }),
}))

describe('MicrosoftLoginButton', () => {
  afterEach(() => {
    vi.unstubAllEnvs()
    initializeMock.mockReset()
    loginPopupMock.mockReset()
  })

  it('renders nothing and never constructs MSAL when no client ID is configured', async () => {
    const { wrapper } = await renderComponent(MicrosoftLoginButton)
    await flushPromises()

    expect(initializeMock).not.toHaveBeenCalled()
    expect(wrapper.find('div').exists()).toBe(false)
  })

  it('initializes and renders an enabled button when a client ID is configured', async () => {
    vi.stubEnv('VITE_MICROSOFT_CLIENT_ID', 'test-client-id')
    initializeMock.mockResolvedValue(undefined)

    const { wrapper } = await renderComponent(MicrosoftLoginButton)
    await flushPromises()

    expect(initializeMock).toHaveBeenCalledTimes(1)
    const button = wrapper.find('button.ms-button')
    expect(button.exists()).toBe(true)
    expect(button.attributes('disabled')).toBeUndefined()
  })

  it('disables the button until MSAL finishes initializing', async () => {
    vi.stubEnv('VITE_MICROSOFT_CLIENT_ID', 'test-client-id')
    let resolveInit!: () => void
    initializeMock.mockReturnValue(
      new Promise<void>((resolve) => {
        resolveInit = resolve
      }),
    )

    const { wrapper } = await renderComponent(MicrosoftLoginButton)
    await flushPromises()

    expect(wrapper.find('button.ms-button').attributes('disabled')).toBeDefined()

    resolveInit()
    await flushPromises()

    expect(wrapper.find('button.ms-button').attributes('disabled')).toBeUndefined()
  })

  it('calls loginPopup() with the expected scopes when clicked', async () => {
    vi.stubEnv('VITE_MICROSOFT_CLIENT_ID', 'test-client-id')
    initializeMock.mockResolvedValue(undefined)
    loginPopupMock.mockRejectedValue(new Error('user cancelled'))

    const { wrapper } = await renderComponent(MicrosoftLoginButton)
    await flushPromises()

    await wrapper.find('button.ms-button').trigger('click')
    await flushPromises()

    expect(loginPopupMock).toHaveBeenCalledWith({ scopes: ['openid', 'profile', 'email'] })
  })
})
