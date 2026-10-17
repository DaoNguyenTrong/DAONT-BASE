import { describe, expect, it } from 'vitest'
import {
  imagePath,
  isAllowedApiRequest,
  missingPreconditions,
  parseLocales,
  type PreconditionData,
} from '../../../user-guide/tools/lib'

const api = 'http://localhost:7000/api'

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
