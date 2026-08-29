import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi } from 'vitest'
import ServerErrorScreen from '@/components/ServerErrorScreen.vue'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

function flushHttp() {
  return new Promise((resolve) => setTimeout(resolve, 0))
}

describe('ServerErrorScreen', () => {
  it('renders the 500 badge, heading and description', async () => {
    const { wrapper } = await renderComponent(ServerErrorScreen)

    expect(wrapper.text()).toContain('500')
    expect(wrapper.text()).toContain('We can’t reach the server')
    expect(wrapper.text()).toContain('Try again')
  })

  it('re-checks the health endpoint when Retry is clicked', async () => {
    const hits = vi.fn()
    server.use(
      http.get('*/api/health', () => {
        hits()
        return HttpResponse.json({
          status: 'healthy',
          version: '1.0.0',
          timestamp: '2026-06-01T10:00:00Z',
          buildTimestamp: null,
        })
      }),
    )

    const { wrapper } = await renderComponent(ServerErrorScreen)
    hits.mockClear()

    // The screen also renders <AppControls> (several buttons) — target Retry by label.
    const retry = wrapper.findAll('button').find((b) => b.text().includes('Try again'))
    await retry!.trigger('click')
    await flushHttp()

    expect(hits).toHaveBeenCalledTimes(1)
  })
})
