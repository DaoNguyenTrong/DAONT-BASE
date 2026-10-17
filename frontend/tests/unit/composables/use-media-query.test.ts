import { afterEach, describe, expect, it, vi } from 'vitest'
import { effectScope } from 'vue'
import { useMediaQuery } from '@/composables/use-media-query'

type Listener = (event: { matches: boolean }) => void

function stubMatchMedia(initial: boolean) {
  const listeners = new Set<Listener>()
  const list = {
    matches: initial,
    addEventListener: (_: string, listener: Listener) => listeners.add(listener),
    removeEventListener: (_: string, listener: Listener) => listeners.delete(listener),
  }
  vi.stubGlobal(
    'matchMedia',
    vi.fn(() => list),
  )
  return {
    listeners,
    change(matches: boolean) {
      list.matches = matches
      listeners.forEach((listener) => listener({ matches }))
    },
  }
}

describe('useMediaQuery', () => {
  afterEach(() => {
    vi.unstubAllGlobals()
  })

  it('reflects the query now and follows later changes', () => {
    const media = stubMatchMedia(true)
    const scope = effectScope()
    const matches = scope.run(() => useMediaQuery('(max-width: 63.9999rem)'))!

    expect(matches.value).toBe(true)
    media.change(false)
    expect(matches.value).toBe(false)
    scope.stop()
  })

  it('stops listening when its scope ends', () => {
    const media = stubMatchMedia(false)
    const scope = effectScope()
    scope.run(() => useMediaQuery('(max-width: 63.9999rem)'))

    scope.stop()

    expect(media.listeners.size).toBe(0)
  })

  it('is false where matchMedia does not exist', () => {
    vi.stubGlobal('matchMedia', undefined)

    expect(useMediaQuery('(max-width: 63.9999rem)').value).toBe(false)
  })
})
