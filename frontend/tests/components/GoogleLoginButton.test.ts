import { flushPromises } from '@vue/test-utils'
import { afterEach, describe, expect, it, vi } from 'vitest'
import GoogleLoginButton from '@/components/GoogleLoginButton.vue'
import { renderComponent } from '../helpers/render'

describe('GoogleLoginButton', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
    vi.unstubAllEnvs()
    delete window.google
  })

  it('renders nothing and never touches window.google when no client ID is configured', async () => {
    const initialize = vi.fn()
    const prompt = vi.fn()
    vi.stubGlobal('google', { accounts: { id: { initialize, prompt } } })

    const { wrapper } = await renderComponent(GoogleLoginButton)
    await flushPromises()

    expect(initialize).not.toHaveBeenCalled()
    expect(wrapper.find('div').exists()).toBe(false)
  })

  it('initializes and renders an enabled custom Google button when a client ID is configured', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'test-client-id')
    const initialize = vi.fn()
    const prompt = vi.fn()
    vi.stubGlobal('google', { accounts: { id: { initialize, prompt } } })

    const { wrapper } = await renderComponent(GoogleLoginButton)
    await flushPromises()

    expect(initialize).toHaveBeenCalledWith(
      expect.objectContaining({ client_id: 'test-client-id', callback: expect.any(Function) }),
    )

    const button = wrapper.find('button.google-button')
    expect(button.exists()).toBe(true)
    expect(button.attributes('disabled')).toBeUndefined()
  })

  it('calls prompt() when the custom Google button is clicked', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'test-client-id')
    const initialize = vi.fn()
    const prompt = vi.fn()
    vi.stubGlobal('google', { accounts: { id: { initialize, prompt } } })

    const { wrapper } = await renderComponent(GoogleLoginButton)
    await flushPromises()

    await wrapper.find('button.google-button').trigger('click')

    expect(prompt).toHaveBeenCalledTimes(1)
  })

  it('disables the button until Google Identity Services is ready', async () => {
    vi.stubEnv('VITE_GOOGLE_CLIENT_ID', 'test-client-id')
    let resolveScript!: () => void
    const scriptReady = new Promise<void>((resolve) => {
      resolveScript = resolve
    })
    vi.stubGlobal('google', undefined)
    const appendChildSpy = vi.spyOn(document.head, 'appendChild').mockImplementation((node) => {
      const script = node as HTMLScriptElement
      void scriptReady.then(() => {
        vi.stubGlobal('google', {
          accounts: { id: { initialize: vi.fn(), prompt: vi.fn() } },
        })
        script.onload?.(new Event('load'))
      })
      return node
    })

    const { wrapper } = await renderComponent(GoogleLoginButton)
    await flushPromises()

    expect(wrapper.find('button.google-button').attributes('disabled')).toBeDefined()

    resolveScript()
    await flushPromises()

    expect(wrapper.find('button.google-button').attributes('disabled')).toBeUndefined()

    appendChildSpy.mockRestore()
  })
})
