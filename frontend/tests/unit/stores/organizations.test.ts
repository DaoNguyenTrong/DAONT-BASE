import { http, HttpResponse } from 'msw'
import { beforeEach, describe, expect, it } from 'vitest'
import { ApiError } from '@/api/types'
import { useOrganizationsStore } from '@/stores/organizations'
import { makeOrganization } from '../../factories/organization'
import { server } from '../../helpers/msw/server'
import { setupTestPinia } from '../../helpers/pinia'

function problemResponse(status: number, title: string) {
  return HttpResponse.json(
    { status, title },
    { status, headers: { 'Content-Type': 'application/problem+json' } },
  )
}

describe('useOrganizationsStore', () => {
  beforeEach(() => {
    setupTestPinia()
  })

  it('starts empty and not loaded', () => {
    const organizations = useOrganizationsStore()

    expect(organizations.myOrganizations).toEqual([])
    expect(organizations.loaded).toBe(false)
  })

  describe('fetchMine', () => {
    it('loads the organizations of the signed-in account and marks the store loaded', async () => {
      server.use(
        http.get('*/api/organizations', () =>
          HttpResponse.json([
            makeOrganization(),
            makeOrganization({ id: 'org-2', name: 'Globex', slug: 'globex' }),
          ]),
        ),
      )
      const organizations = useOrganizationsStore()

      const result = await organizations.fetchMine()

      expect(result.map((o) => o.id)).toEqual(['org-1', 'org-2'])
      expect(organizations.myOrganizations).toEqual(result)
      expect(organizations.loaded).toBe(true)
    })

    it('marks the store loaded even when the account has no organizations', async () => {
      server.use(http.get('*/api/organizations', () => HttpResponse.json([])))
      const organizations = useOrganizationsStore()

      await organizations.fetchMine()

      expect(organizations.myOrganizations).toEqual([])
      expect(organizations.loaded).toBe(true)
    })

    it('replaces previously loaded organizations rather than appending', async () => {
      server.use(
        http.get('*/api/organizations', () =>
          HttpResponse.json([makeOrganization({ id: 'org-3', name: 'Initech' })]),
        ),
      )
      const organizations = useOrganizationsStore()
      organizations.myOrganizations = [makeOrganization()]

      await organizations.fetchMine()

      expect(organizations.myOrganizations.map((o) => o.id)).toEqual(['org-3'])
    })

    it('rejects with an ApiError and leaves state untouched when the request fails', async () => {
      server.use(http.get('*/api/organizations', () => problemResponse(500, 'Server error')))
      const organizations = useOrganizationsStore()
      const existing = [makeOrganization()]
      organizations.myOrganizations = existing

      await expect(organizations.fetchMine()).rejects.toBeInstanceOf(ApiError)
      expect(organizations.myOrganizations).toEqual(existing)
      expect(organizations.loaded).toBe(false)
    })
  })

  describe('create', () => {
    it('posts the request and appends the created organization', async () => {
      let body: unknown
      server.use(
        http.post('*/api/organizations', async ({ request }) => {
          body = await request.json()
          return HttpResponse.json(
            makeOrganization({ id: 'org-new', name: 'Globex', slug: 'globex' }),
            { status: 201 },
          )
        }),
      )
      const organizations = useOrganizationsStore()
      organizations.myOrganizations = [makeOrganization()]

      const created = await organizations.create({ name: 'Globex', slug: 'globex' })

      expect(body).toEqual({ name: 'Globex', slug: 'globex' })
      expect(created.id).toBe('org-new')
      expect(organizations.myOrganizations.map((o) => o.id)).toEqual(['org-1', 'org-new'])
    })

    it('rejects and leaves state untouched when the server refuses the organization', async () => {
      server.use(http.post('*/api/organizations', () => problemResponse(409, 'Conflict')))
      const organizations = useOrganizationsStore()
      organizations.myOrganizations = [makeOrganization()]

      await expect(organizations.create({ name: 'Acme', slug: 'acme' })).rejects.toMatchObject({
        name: 'ApiError',
        status: 409,
      })
      expect(organizations.myOrganizations.map((o) => o.id)).toEqual(['org-1'])
    })
  })

  describe('deactivate', () => {
    it('deletes the organization and drops only the matching one from state', async () => {
      let deletedId: string | undefined
      server.use(
        http.delete('*/api/organizations/:id', ({ params }) => {
          deletedId = params.id as string
          return new HttpResponse(null, { status: 204 })
        }),
      )
      const organizations = useOrganizationsStore()
      organizations.myOrganizations = [
        makeOrganization(),
        makeOrganization({ id: 'org-2', name: 'Globex' }),
        makeOrganization({ id: 'org-3', name: 'Initech' }),
      ]

      await organizations.deactivate('org-2')

      expect(deletedId).toBe('org-2')
      expect(organizations.myOrganizations.map((o) => o.id)).toEqual(['org-1', 'org-3'])
    })

    it('leaves state unchanged when the id is not in the list', async () => {
      server.use(
        http.delete('*/api/organizations/:id', () => new HttpResponse(null, { status: 204 })),
      )
      const organizations = useOrganizationsStore()
      organizations.myOrganizations = [makeOrganization()]

      await organizations.deactivate('org-missing')

      expect(organizations.myOrganizations.map((o) => o.id)).toEqual(['org-1'])
    })

    it('keeps the organization in state when the request fails', async () => {
      server.use(http.delete('*/api/organizations/:id', () => problemResponse(403, 'Forbidden')))
      const organizations = useOrganizationsStore()
      organizations.myOrganizations = [makeOrganization()]

      await expect(organizations.deactivate('org-1')).rejects.toBeInstanceOf(ApiError)
      expect(organizations.myOrganizations.map((o) => o.id)).toEqual(['org-1'])
    })
  })
})
