import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { afterEach, beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest'
import GuideAuthView from '@/views/GuideAuthView.vue'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'
import en from '@/locales/en'

describe('GuideAuthView', () => {
  // A spy, not a replaced window.location: the relative fetch resolves against location.href.
  let replace: MockInstance<Location['replace']>

  beforeEach(() => {
    replace = vi.spyOn(window.location, 'replace').mockImplementation(() => {})
  })

  afterEach(() => {
    replace.mockRestore()
  })

  async function open(next: string) {
    const { wrapper } = await renderComponent(GuideAuthView, {
      initialRoute: `/guide-auth?next=${encodeURIComponent(next)}`,
    })
    await flushPromises()
    return wrapper
  }

  it('opens the requested guide page once the session check passes', async () => {
    server.use(http.get('*/api/auth/check', () => new HttpResponse(null, { status: 204 })))

    await open('/guide/organizations/add-member')

    expect(replace).toHaveBeenCalledWith('/guide/organizations/add-member')
  })

  it('falls back to the guide home for an unsafe next', async () => {
    server.use(http.get('*/api/auth/check', () => new HttpResponse(null, { status: 204 })))

    await open('//evil.example')

    expect(replace).toHaveBeenCalledWith('/guide/')
  })

  // Redirecting anyway would send the reader straight back here from nginx: an endless loop.
  it('shows an error instead of redirecting when the check fails', async () => {
    server.use(http.get('*/api/auth/check', () => new HttpResponse(null, { status: 401 })))

    const wrapper = await open('/guide/faq')

    expect(wrapper.text()).toContain(en.guideAuth.failed)
    expect(replace).not.toHaveBeenCalled()
  })

  it('shows an error when the API is unreachable', async () => {
    server.use(http.get('*/api/auth/check', () => HttpResponse.error()))

    const wrapper = await open('/guide/faq')

    expect(wrapper.text()).toContain(en.guideAuth.failed)
    expect(replace).not.toHaveBeenCalled()
  })
})
