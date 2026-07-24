import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it } from 'vitest'
import ResendVerificationForm from '@/components/ResendVerificationForm.vue'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

function problemResponse(body: Record<string, unknown>, status: number) {
  return HttpResponse.json(body, {
    status,
    headers: { 'Content-Type': 'application/problem+json' },
  })
}

describe('ResendVerificationForm', () => {
  it('shows a validation error when submitting with an empty email', async () => {
    const { wrapper } = await renderComponent(ResendVerificationForm)

    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Email is required.')
  })

  it('shows a success message after a successful resend', async () => {
    let requestBody: Record<string, unknown> | undefined
    server.use(
      http.post('*/api/auth/resend-verification', async ({ request }) => {
        requestBody = (await request.json()) as Record<string, unknown>
        return new HttpResponse(null, { status: 204 })
      }),
    )

    const { wrapper } = await renderComponent(ResendVerificationForm)
    await wrapper.find('input#resend-verification-email').setValue('user@example.com')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(requestBody?.email).toBe('user@example.com')
    expect(wrapper.text()).toContain(
      'If an account exists for this email, a verification link has been sent.',
    )
    expect(wrapper.find('form').exists()).toBe(false)
  })

  it('shows an error message when the resend request fails', async () => {
    server.use(
      http.post('*/api/auth/resend-verification', () =>
        problemResponse(
          { status: 429, title: 'Too Many Requests', detail: 'Slow down.', code: 'TooManyRequests' },
          429,
        ),
      ),
    )

    const { wrapper } = await renderComponent(ResendVerificationForm)
    await wrapper.find('input#resend-verification-email').setValue('user@example.com')
    await wrapper.find('form').trigger('submit')
    await flushPromises()

    expect(wrapper.text()).toContain('Slow down.')
  })
})
