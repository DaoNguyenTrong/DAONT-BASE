import { mkdtempSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { describe, expect, it } from 'vitest'
import {
  imagePath,
  markFits,
  markPosition,
  readSessionFailure,
  recordSessionFailure,
  isAllowedApiRequest,
  missingPreconditions,
  parseLocales,
  type PreconditionData,
} from '../../../user-guide/tools/lib'

const api = 'http://localhost:7100/api'

describe('parseLocales', () => {
  it.each([undefined, '', 'all'])('captures both languages for %j', (value) => {
    expect(parseLocales(value)).toEqual(['vi', 'en'])
  })

  it('captures one language', () => {
    expect(parseLocales('en')).toEqual(['en'])
  })

  it.each(['VI', 'fr'])('rejects %j instead of capturing nothing', (value) => {
    expect(() => parseLocales(value)).toThrow('vi|en|all')
  })
})

describe('isAllowedApiRequest', () => {
  it.each([
    ['GET', `${api}/organizations`],
    ['POST', `${api}/auth/login`],
    ['POST', `${api}/auth/refresh?x=1`],
  ])('lets %s %s through', (method, url) => {
    expect(isAllowedApiRequest(method, url)).toBe(true)
  })

  it.each([
    ['POST', `${api}/auth/logout`],
    ['PATCH', `${api}/notifications/read-all`],
    ['DELETE', `${api}/auth/sessions/5`],
    ['POST', `${api}/auth/login/extra`],
    ['POST', `${api}/auth/switch-organization`],
    ['PUT', `${api}/profile`],
  ])('blocks %s %s', (method, url) => {
    expect(isAllowedApiRequest(method, url)).toBe(false)
  })
})

describe('imagePath', () => {
  const base = { siteSrc: '/site/src', tree: 'base' as const, locale: 'vi' as const }

  it('writes under the tree, locale and chapter', () => {
    expect(imagePath({ ...base, chapter: 'organizations', name: 'add-member' })).toBe(
      '/site/src/base/images/vi/organizations/add-member.jpg',
    )
  })

  it('allows a two-level chapter', () => {
    expect(
      imagePath({ ...base, tree: 'app', chapter: 'organizations/members', name: 'list' }),
    ).toBe('/site/src/app/images/vi/organizations/members/list.jpg')
  })

  it.each([
    ['../x', 'a'],
    ['a/b/c', 'a'],
    ['Org', 'a'],
    ['organizations', 'a/b'],
    ['organizations', '..'],
  ])('refuses chapter %j with name %j', (chapter, name) => {
    expect(() => imagePath({ ...base, chapter, name })).toThrow()
  })
})

describe('missingPreconditions', () => {
  const ready: PreconditionData = {
    organizations: [{ id: 'o1', myPermissionCodes: ['organizations.manage'] }],
    membersByOrg: { o1: 2 },
    customRolesByOrg: { o1: 1 },
    unreadNotifications: 1,
    sessions: 2,
  }

  it('passes when every condition holds', () => {
    expect(missingPreconditions(ready)).toEqual([])
  })

  it('lists every unmet condition at once', () => {
    const missing = missingPreconditions({
      organizations: [],
      membersByOrg: {},
      customRolesByOrg: {},
      unreadNotifications: 0,
      sessions: 1,
    })

    expect(missing).toHaveLength(4)
    expect(missing.join('\n')).toMatch(/owner/i)
    expect(missing.join('\n')).toMatch(/notification/i)
    expect(missing.join('\n')).toMatch(/session/i)
  })

  it('names only the members when the owned organization has one', () => {
    const missing = missingPreconditions({ ...ready, membersByOrg: { o1: 1 } })

    expect(missing).toHaveLength(1)
    expect(missing[0]).toMatch(/member/i)
  })

  it('ignores organizations the account does not own', () => {
    const missing = missingPreconditions({
      ...ready,
      organizations: [
        { id: 'o1', myPermissionCodes: [] },
        { id: 'o2', myPermissionCodes: ['organizations.manage'] },
      ],
      membersByOrg: { o1: 5, o2: 1 },
      customRolesByOrg: { o1: 3, o2: 0 },
    })

    expect(missing).toHaveLength(1)
    expect(missing[0]).toMatch(/member/i)
  })
})

describe('markPosition', () => {
  const box = { x: 100, y: 50, width: 200, height: 40 }

  it('sits inside the top-left corner by default', () => {
    expect(markPosition(box, 'top-left')).toEqual({ x: 104, y: 54, size: 28 })
  })

  it('sits inside the top-right corner', () => {
    expect(markPosition(box, 'top-right')).toEqual({ x: 268, y: 54, size: 28 })
  })

  it('sits just right of the element, centred', () => {
    expect(markPosition(box, 'after')).toEqual({ x: 308, y: 56, size: 28 })
  })

  it('sits just under the element, centred', () => {
    expect(markPosition(box, 'below')).toEqual({ x: 186, y: 96, size: 28 })
  })

  it('sits just left of the element, centred', () => {
    expect(markPosition(box, 'before')).toEqual({ x: 64, y: 56, size: 28 })
  })
})

describe('markFits', () => {
  const area = { x: 0, y: 0, width: 1440, height: 900 }

  it('accepts a mark inside the shot', () => {
    expect(markFits({ x: 10, y: 10, size: 28 }, area)).toBe(true)
  })

  // A target below the fold passes toBeVisible() yet its mark would be cut off the image.
  it('rejects a mark outside the shot', () => {
    expect(markFits({ x: 10, y: 1200, size: 28 }, area)).toBe(false)
    expect(markFits({ x: 1430, y: 10, size: 28 }, area)).toBe(false)
  })

  it('measures against a clipped area', () => {
    const clip = { x: 300, y: 200, width: 400, height: 300 }
    expect(markFits({ x: 320, y: 220, size: 28 }, clip)).toBe(true)
    expect(markFits({ x: 100, y: 220, size: 28 }, clip)).toBe(false)
  })
})

describe('session failure marker', () => {
  // Playwright restarts a worker after a worker fixture fails; the next one must not sign in again.
  it('hands the first failure to later workers of the same run', () => {
    const dir = mkdtempSync(join(tmpdir(), 'guide-capture-'))

    recordSessionFailure(dir, 'run-1', 'Wrong password')

    expect(readSessionFailure(dir, 'run-1')).toBe('Wrong password')
  })

  it('does not leak into another run', () => {
    const dir = mkdtempSync(join(tmpdir(), 'guide-capture-'))

    recordSessionFailure(dir, 'run-1', 'Wrong password')

    expect(readSessionFailure(dir, 'run-2')).toBeNull()
  })
})
