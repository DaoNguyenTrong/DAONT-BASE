import { describe, it, expect, beforeEach, vi } from 'vitest'

const mockCreate = vi.hoisted(() => vi.fn())

vi.mock('naive-ui', async (importOriginal) => {
  const actual = await importOriginal<typeof import('naive-ui')>()
  return { ...actual, useDialog: () => ({ create: mockCreate }) }
})

// Mocking naive-ui's useDialog() context sidesteps mounting a real
// <n-dialog-provider> + teleported DOM (fragile, first-of-its-kind here) —
// we capture the options object passed to dialog.create(...) and drive its
// callbacks directly as plain function calls instead.
const { useAppDialogNaive } = await import('@/composables/use-app-dialog-naive')
const { withSetup } = await import('../../helpers/with-setup')

function makeInstance() {
  return { destroy: vi.fn(), loading: false }
}

describe('useAppDialogNaive', () => {
  beforeEach(() => {
    mockCreate.mockReset()
  })

  it('calls dialog.create with the header as title and the dialogClass', async () => {
    mockCreate.mockReturnValue(makeInstance())
    const { result } = await withSetup(() => useAppDialogNaive())

    result.open(
      { name: 'Content' } as never,
      { header: 'Delete item', dialogClass: 'my-dialog', onConfirm: async () => {} },
    )

    expect(mockCreate).toHaveBeenCalledTimes(1)
    const options = mockCreate.mock.calls[0][0]
    expect(options.title).toBe('Delete item')
    expect(options.class).toBe('my-dialog')
  })

  it('falls back to i18n common.save/common.cancel when labels are omitted', async () => {
    mockCreate.mockReturnValue(makeInstance())
    const { result } = await withSetup(() => useAppDialogNaive())

    result.open({} as never, { header: 'x', onConfirm: async () => {} })

    const options = mockCreate.mock.calls[0][0]
    expect(options.positiveText).toBe('Save')
    expect(options.negativeText).toBe('Cancel')
  })

  it('uses confirmLabel/cancelLabel when provided', async () => {
    mockCreate.mockReturnValue(makeInstance())
    const { result } = await withSetup(() => useAppDialogNaive())

    result.open(
      {} as never,
      { header: 'x', confirmLabel: 'Yes, delete', cancelLabel: 'Nope', onConfirm: async () => {} },
    )

    const options = mockCreate.mock.calls[0][0]
    expect(options.positiveText).toBe('Yes, delete')
    expect(options.negativeText).toBe('Nope')
  })

  it('onPositiveClick awaits onConfirm, toggling instance.loading around it', async () => {
    const instance = makeInstance()
    mockCreate.mockReturnValue(instance)
    const { result } = await withSetup(() => useAppDialogNaive())

    let resolveConfirm!: () => void
    const onConfirm = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveConfirm = resolve
        }),
    )
    result.open({} as never, { header: 'x', onConfirm })

    const options = mockCreate.mock.calls[0][0]
    const clickResult = options.onPositiveClick()

    expect(instance.loading).toBe(true)
    resolveConfirm()
    expect(await clickResult).toBe(false)
    expect(instance.loading).toBe(false)
  })

  it('guards against double-submit while onConfirm is still pending', async () => {
    mockCreate.mockReturnValue(makeInstance())
    const { result } = await withSetup(() => useAppDialogNaive())

    let resolveConfirm!: () => void
    const onConfirm = vi.fn(
      () =>
        new Promise<void>((resolve) => {
          resolveConfirm = resolve
        }),
    )
    result.open({} as never, { header: 'x', onConfirm })

    const options = mockCreate.mock.calls[0][0]
    const first = options.onPositiveClick()
    const second = await options.onPositiveClick()

    expect(second).toBe(false)
    expect(onConfirm).toHaveBeenCalledTimes(1)

    resolveConfirm()
    await first
  })

  it('resets loading to false even when onConfirm rejects', async () => {
    const instance = makeInstance()
    mockCreate.mockReturnValue(instance)
    const { result } = await withSetup(() => useAppDialogNaive())

    const onConfirm = vi.fn(() => Promise.reject(new Error('boom')))
    result.open({} as never, { header: 'x', onConfirm })

    const options = mockCreate.mock.calls[0][0]
    await expect(options.onPositiveClick()).rejects.toThrow('boom')
    expect(instance.loading).toBe(false)
  })

  it('close() destroys the dialog instance', async () => {
    const instance = makeInstance()
    mockCreate.mockReturnValue(instance)
    const { result } = await withSetup(() => useAppDialogNaive())

    result.open({} as never, { header: 'x', onConfirm: async () => {} })

    const options = mockCreate.mock.calls[0][0]
    // `close` is exposed as a prop to the rendered content component's vnode.
    const vnode = options.content()
    vnode.props.close()

    expect(instance.destroy).toHaveBeenCalledTimes(1)
  })

  it('onNegativeClick calls onCancel when provided', async () => {
    mockCreate.mockReturnValue(makeInstance())
    const { result } = await withSetup(() => useAppDialogNaive())
    const onCancel = vi.fn()

    result.open({} as never, { header: 'x', onConfirm: async () => {}, onCancel })

    const options = mockCreate.mock.calls[0][0]
    options.onNegativeClick()

    expect(onCancel).toHaveBeenCalledTimes(1)
  })

  it('onNegativeClick is a safe no-op when onCancel is omitted', async () => {
    mockCreate.mockReturnValue(makeInstance())
    const { result } = await withSetup(() => useAppDialogNaive())

    result.open({} as never, { header: 'x', onConfirm: async () => {} })

    const options = mockCreate.mock.calls[0][0]
    expect(() => options.onNegativeClick()).not.toThrow()
  })

  it('onClose calls onCancel the same way as onNegativeClick', async () => {
    mockCreate.mockReturnValue(makeInstance())
    const { result } = await withSetup(() => useAppDialogNaive())
    const onCancel = vi.fn()

    result.open({} as never, { header: 'x', onConfirm: async () => {}, onCancel })

    const options = mockCreate.mock.calls[0][0]
    options.onClose()

    expect(onCancel).toHaveBeenCalledTimes(1)
  })
})
