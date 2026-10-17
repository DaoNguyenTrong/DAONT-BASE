import { describe, expect, mock, test } from 'bun:test'
import {
  createAuthGuard,
  reloadThroughServer,
  type GuardDeps,
} from '../.vitepress/theme/auth-guard'

function setup(overrides: Partial<GuardDeps> = {}) {
  const deps = {
    mode: mock(async () => 'private'),
    check: mock(async () => 204),
    navigate: mock((_to: string) => {}),
    ...overrides,
  }
  return { deps, guard: createAuthGuard(deps) }
}

describe('createAuthGuard', () => {
  test('skips the first navigation (the page nginx just served)', async () => {
    const { deps, guard } = setup()

    await guard('/guide/')

    expect(deps.check).not.toHaveBeenCalled()
  })

  test('lets navigation through in public mode without checking', async () => {
    const { deps, guard } = setup({ mode: mock(async () => 'public') })

    await guard('/guide/')
    const result = await guard('/guide/faq')

    expect(result).not.toBe(false)
    expect(deps.check).not.toHaveBeenCalled()
  })

  test('reloads the target through nginx when the session has lapsed', async () => {
    const { deps, guard } = setup({ check: mock(async () => 401) })

    await guard('/guide/')
    const result = await guard('/guide/faq')

    expect(result).toBe(false)
    expect(deps.navigate).toHaveBeenCalledWith('/guide/faq')
  })

  test('lets navigation through when the session is valid', async () => {
    const { deps, guard } = setup()

    await guard('/guide/')
    const result = await guard('/guide/faq')

    expect(result).not.toBe(false)
    expect(deps.check).toHaveBeenCalledTimes(1)
    expect(deps.navigate).not.toHaveBeenCalled()
  })

  // The next full page load still goes through nginx, which decides.
  test('lets navigation through when the network fails', async () => {
    const { deps, guard } = setup({
      check: mock(async () => {
        throw new TypeError('Failed to fetch')
      }),
    })

    await guard('/guide/')
    const result = await guard('/guide/faq')

    expect(result).not.toBe(false)
    expect(deps.navigate).not.toHaveBeenCalled()
  })

  test('lets navigation through when the mode cannot be read', async () => {
    const { deps, guard } = setup({
      mode: mock(async () => {
        throw new TypeError('Failed to fetch')
      }),
    })

    await guard('/guide/')
    const result = await guard('/guide/faq')

    expect(result).not.toBe(false)
    expect(deps.navigate).not.toHaveBeenCalled()
  })

  test('fetches the mode only once', async () => {
    const { deps, guard } = setup()

    await guard('/guide/')
    await guard('/guide/faq')
    await guard('/guide/getting-started/')
    await guard('/guide/en/faq')

    expect(deps.mode).toHaveBeenCalledTimes(1)
  })
})

describe('reloadThroughServer', () => {
  // VitePress has already pushed the target URL; assigning that same URL with a #fragment is only a
  // scroll, not a load — the reader would stay on the old page with the new URL.
  test('drops the fragment so the browser really loads the page', () => {
    const replace = mock((_url: string) => {})

    reloadThroughServer('/guide/faq#where-to-start', { replace })

    expect(replace).toHaveBeenCalledWith('/guide/faq')
  })

  test('loads a target without a fragment as is', () => {
    const replace = mock((_url: string) => {})

    reloadThroughServer('/guide/faq?x=1', { replace })

    expect(replace).toHaveBeenCalledWith('/guide/faq?x=1')
  })
})
