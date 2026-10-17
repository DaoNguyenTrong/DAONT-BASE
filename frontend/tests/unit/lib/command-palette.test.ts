import { afterEach, describe, expect, it } from 'vitest'
import { Home } from '@vicons/tabler'
import {
  filterPaletteResults,
  registerPaletteAction,
  usePaletteActions,
  type PaletteResult,
} from '@/lib/command-palette'
import i18n from '@/plugins/i18n'

const t = i18n.global.t as (key: string) => string

function page(labelKey: string, trailKeys: string[] = []): PaletteResult {
  return { kind: 'page', id: labelKey, labelKey, icon: Home, trailKeys }
}

describe('filterPaletteResults', () => {
  it('returns everything for an empty query', () => {
    const results = [page('nav.home'), page('nav.organizations')]

    expect(filterPaletteResults(results, '', t)).toHaveLength(2)
  })

  it('matches without accents', () => {
    const results = [page('nav.home'), page('nav.organizations')]

    const found = filterPaletteResults(results, 'to chuc', (key) =>
      key === 'nav.organizations' ? 'Tổ chức' : 'Trang chủ',
    )

    expect(found.map((r) => r.id)).toEqual(['nav.organizations'])
  })

  it('matches the parent trail', () => {
    const results = [page('nav.home', ['nav.organizations']), page('nav.profile')]

    const found = filterPaletteResults(results, 'organizations', (key) => `label ${key}`)

    expect(found.map((r) => r.id)).toEqual(['nav.home'])
  })

  it('matches keywords', () => {
    const result: PaletteResult = { ...page('nav.home'), kind: 'action', keywords: ['exit'] }

    expect(filterPaletteResults([result], 'exit', () => 'Zzz')).toHaveLength(1)
  })

  it('lists pages before actions', () => {
    const action: PaletteResult = { ...page('auth.logout'), kind: 'action' }

    const found = filterPaletteResults([action, page('nav.home')], '', t)

    expect(found.map((r) => r.kind)).toEqual(['page', 'action'])
  })
})

describe('palette action registry', () => {
  const cleanups: (() => void)[] = []

  afterEach(() => {
    cleanups.splice(0).forEach((cleanup) => cleanup())
  })

  function register(id: string, run = () => {}) {
    const unregister = registerPaletteAction({ id, labelKey: 'nav.home', icon: Home, run })
    cleanups.push(unregister)
    return unregister
  }

  it('replaces an action registered twice with the same id', () => {
    register('dup')
    const second = () => {}
    register('dup', second)

    const matches = usePaletteActions().value.filter((a) => a.id === 'dup')
    expect(matches).toHaveLength(1)
    expect(matches[0]!.run).toBe(second)
  })

  it('unregisters an action', () => {
    const unregister = register('gone')

    unregister()

    expect(usePaletteActions().value.some((a) => a.id === 'gone')).toBe(false)
  })
})
