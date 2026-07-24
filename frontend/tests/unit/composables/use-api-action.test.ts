import { describe, expect, it, vi } from 'vitest'
import { useApiAction } from '@/composables/use-api-action'
import { ApiError } from '@/api/types'
import { withSetup } from '../../helpers/with-setup'

describe('useApiAction', () => {
  it('returns the resolved value on success and clears prior state', async () => {
    const { result, app } = await withSetup(() => useApiAction())

    const value = await result.run(() => Promise.resolve('ok'))

    expect(value).toBe('ok')
    expect(result.error.value).toBeNull()
    expect(result.fieldErrors.value).toEqual({})
    expect(result.isLoading.value).toBe(false)

    app.unmount()
  })

  it('maps ValidationFailed onto fieldErrors via mapValidationErrors', async () => {
    const { result, app } = await withSetup(() => useApiAction())

    const value = await result.run(() =>
      Promise.reject(
        new ApiError({
          status: 400,
          title: 'Bad Request',
          code: 'ValidationFailed',
          errors: { Email: ['Email is required.'] },
        }),
      ),
    )

    expect(value).toBeUndefined()
    expect(result.fieldErrors.value).toEqual({ email: 'Email is required.' })
    expect(result.error.value).toBeNull()

    app.unmount()
  })

  it('invokes the matching onCode handler instead of setting error', async () => {
    const { result, app } = await withSetup(() => useApiAction())
    const handler = vi.fn()

    await result.run(
      () =>
        Promise.reject(
          new ApiError({ status: 409, title: 'Conflict', code: 'AccountUsernameAlreadyExists' }),
        ),
      { onCode: { AccountUsernameAlreadyExists: handler } },
    )

    expect(handler).toHaveBeenCalledWith(undefined)
    expect(result.error.value).toBeNull()

    app.unmount()
  })

  it('falls back to problem.detail when no onCode handler matches', async () => {
    const { result, app } = await withSetup(() => useApiAction())

    await result.run(() =>
      Promise.reject(
        new ApiError({ status: 401, title: 'Unauthorized', detail: 'Nope.', code: 'SomeOtherCode' }),
      ),
    )

    expect(result.error.value).toBe('Nope.')

    app.unmount()
  })

  it('falls back to fallbackMessage for a non-ApiError throw', async () => {
    const { result, app } = await withSetup(() => useApiAction())

    await result.run(() => Promise.reject(new Error('boom')), { fallbackMessage: 'Something broke.' })

    expect(result.error.value).toBe('Something broke.')

    app.unmount()
  })

  it('ignores a concurrent run() call while isLoading is true', async () => {
    const { result, app } = await withSetup(() => useApiAction())
    let resolveFirst!: (value: string) => void
    const first = new Promise<string>((resolve) => {
      resolveFirst = resolve
    })
    const secondAction = vi.fn(() => Promise.resolve('second'))

    const firstCall = result.run(() => first)
    const secondCall = result.run(secondAction)

    expect(await secondCall).toBeUndefined()
    expect(secondAction).not.toHaveBeenCalled()

    resolveFirst('first')
    expect(await firstCall).toBe('first')

    app.unmount()
  })

  it('clearFieldError removes only the named field', async () => {
    const { result, app } = await withSetup(() => useApiAction())

    await result.run(() =>
      Promise.reject(
        new ApiError({
          status: 400,
          title: 'Bad Request',
          code: 'ValidationFailed',
          errors: { name: ['Required'], email: ['Required'] },
        }),
      ),
    )
    expect(result.fieldErrors.value).toEqual({ name: 'Required', email: 'Required' })

    result.clearFieldError('name')

    expect(result.fieldErrors.value).toEqual({ email: 'Required' })

    app.unmount()
  })
})
