import { flushPromises } from '@vue/test-utils'
import { http, HttpResponse } from 'msw'
import { describe, expect, it, vi, beforeEach, afterEach } from 'vitest'
import AccountsView from '@/views/AccountsView.vue'
import type {
  Account,
  AccountPagedResult,
  CreateAccountRequest,
  UpdateAccountRequest,
} from '@/api/types'
import { server } from '../helpers/msw/server'
import { renderComponent } from '../helpers/render'

const mockOpen = vi.hoisted(() => vi.fn())

vi.mock('@/composables/use-app-dialog-naive', () => ({
  useAppDialogNaive: () => ({ open: mockOpen }),
}))

// naive-ui's NVirtualList only renders items it computes as "visible" from
// real layout measurements (container height via ResizeObserver) — happy-dom
// has no layout engine, so the container always measures 0 and the list
// renders nothing. Stub it with a plain v-for so business logic (search,
// scroll, CRUD dialogs) can be tested without depending on real virtualization.
//
// unplugin-vue-components' NaiveUiResolver inlines `<NVirtualList>` as a
// direct `import { NVirtualList } from 'naive-ui'` at compile time — it
// never goes through Vue's `resolveComponent`, so `global.stubs` (which only
// intercepts dynamically-resolved components) cannot reach it. Mocking the
// `naive-ui` module itself (same technique as
// `use-app-dialog-naive.test.ts`'s `useDialog` mock) is the only seam that
// actually replaces what AccountsView.vue's compiled render function calls.
const NVirtualListStub = vi.hoisted(() => ({
  props: ['items', 'itemSize', 'keyField'],
  emits: ['scroll'],
  template: `<div class="overflow-y-auto" @scroll="$emit('scroll', $event)">
    <div v-for="item in items" :key="item[keyField]">
      <slot name="default" :item="item" />
    </div>
  </div>`,
}))

vi.mock('naive-ui', async (importOriginal) => {
  const actual = await importOriginal<typeof import('naive-ui')>()
  return { ...actual, NVirtualList: NVirtualListStub }
})

function makeAccount(overrides: Partial<Account> = {}): Account {
  return {
    id: '1',
    name: 'Alice Nguyen',
    username: 'alice',
    email: 'alice@example.com',
    emailConfirmed: true,
    phone: null,
    position: null,
    address: null,
    status: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    ...overrides,
  }
}

function makePage(items: Account[], totalCount: number): AccountPagedResult {
  return {
    items,
    totalCount,
    pageNumber: 0,
    pageSize: 10,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  }
}

function mockAccountsPage(handler: (url: URL) => AccountPagedResult) {
  server.use(
    http.get('*/api/accounts', ({ request }) => {
      const url = new URL(request.url)
      return HttpResponse.json(handler(url))
    }),
  )
}

async function renderAccountsView() {
  const result = await renderComponent(AccountsView)
  await flushPromises()
  return result
}

describe('AccountsView', () => {
  beforeEach(() => {
    mockOpen.mockReset()
  })

  afterEach(() => {
    vi.useRealTimers()
  })

  describe('load & display', () => {
    it('renders accounts returned from the first page', async () => {
      mockAccountsPage(() =>
        makePage([makeAccount({ name: 'Alice Nguyen', username: 'alice' })], 1),
      )

      const { wrapper } = await renderAccountsView()

      expect(wrapper.text()).toContain('Alice Nguyen')
      expect(wrapper.text()).toContain('alice')
    })

    it('shows the empty state when there are no accounts', async () => {
      mockAccountsPage(() => makePage([], 0))

      const { wrapper } = await renderAccountsView()

      expect(wrapper.text()).toContain('No accounts yet')
    })
  })

  describe('search debounce', () => {
    it('debounces search input and refetches only once after 300ms', async () => {
      let calls = 0
      mockAccountsPage((url) => {
        calls++
        const search = url.searchParams.get('search')
        return makePage(
          [
            makeAccount({
              id: String(calls),
              name: search ? `Filtered ${search}` : 'Alice Nguyen',
            }),
          ],
          1,
        )
      })

      const { wrapper } = await renderAccountsView()
      expect(calls).toBe(1)

      vi.useFakeTimers()
      const input = wrapper.find('input')
      await input.setValue('ali')
      await wrapper.find('input').trigger('input')

      await vi.advanceTimersByTimeAsync(150)
      expect(calls).toBe(1)

      await input.setValue('alice')
      await wrapper.find('input').trigger('input')
      await vi.advanceTimersByTimeAsync(150)
      expect(calls).toBe(1)

      await vi.advanceTimersByTimeAsync(300)
      expect(calls).toBe(2)
    })

    it('clears the search and refetches immediately on clear', async () => {
      let lastSearch: string | null = null
      let calls = 0
      mockAccountsPage((url) => {
        calls++
        lastSearch = url.searchParams.get('search')
        return makePage([makeAccount()], 1)
      })

      const { wrapper } = await renderAccountsView()
      expect(calls).toBe(1)

      // Fake timers here (not real ones) so the debounce `setTimeout` set by
      // `onSearchInput` below is fully discarded by `vi.useRealTimers()` in
      // `afterEach` — `clearSearch()` does NOT cancel that pending timeout
      // (a latent bug in AccountsView.vue, out of scope for this test), so
      // with real timers it fires ~300ms later during an unrelated test and
      // pollutes that test's request count.
      vi.useFakeTimers()
      const input = wrapper.find('input')
      await input.setValue('alice')
      await input.trigger('input')

      const clearIcon = wrapper.find('.cursor-pointer')
      expect(clearIcon.exists()).toBe(true)
      await clearIcon.trigger('click')
      await flushPromises()

      // The debounced fetch from typing 'alice' is never advanced (still
      // pending on the fake timer), so this second call is only explained
      // by clearSearch() triggering its own immediate list.reset().
      expect(calls).toBe(2)
      expect(lastSearch).toBeNull()
    })
  })

  describe('infinite scroll', () => {
    it('loads the next page when scrolled near the bottom', async () => {
      const requestedPages: number[] = []
      server.use(
        http.get('*/api/accounts', ({ request }) => {
          const url = new URL(request.url)
          const pageNumber = Number(url.searchParams.get('pageNumber'))
          requestedPages.push(pageNumber)
          const items = pageNumber === 1 ? [makeAccount({ id: '1' })] : [makeAccount({ id: '2' })]
          return HttpResponse.json(makePage(items, 2))
        }),
      )

      const { wrapper } = await renderAccountsView()
      expect(requestedPages).toEqual([1])

      const scrollContainer = wrapper.find('[class*="overflow-y-auto"]')
      expect(scrollContainer.exists()).toBe(true)

      Object.defineProperty(scrollContainer.element, 'scrollHeight', {
        value: 1000,
        configurable: true,
      })
      Object.defineProperty(scrollContainer.element, 'clientHeight', {
        value: 400,
        configurable: true,
      })
      Object.defineProperty(scrollContainer.element, 'scrollTop', {
        value: 850,
        configurable: true,
      })

      await scrollContainer.trigger('scroll')
      await flushPromises()

      expect(requestedPages).toEqual([1, 2])
    })

    it('does not load further pages once hasMore is false', async () => {
      let calls = 0
      mockAccountsPage(() => {
        calls++
        return makePage([makeAccount()], 1)
      })

      const { wrapper } = await renderAccountsView()
      expect(calls).toBe(1)

      const scrollContainer = wrapper.find('[class*="overflow-y-auto"]')
      Object.defineProperty(scrollContainer.element, 'scrollHeight', {
        value: 1000,
        configurable: true,
      })
      Object.defineProperty(scrollContainer.element, 'clientHeight', {
        value: 400,
        configurable: true,
      })
      Object.defineProperty(scrollContainer.element, 'scrollTop', {
        value: 850,
        configurable: true,
      })

      await scrollContainer.trigger('scroll')
      await flushPromises()

      expect(calls).toBe(1)
    })
  })

  describe('create dialog', () => {
    it('opens the create dialog with default empty state', async () => {
      mockAccountsPage(() => makePage([], 0))
      const { wrapper } = await renderAccountsView()

      await wrapper.find('button').trigger('click')

      expect(mockOpen).toHaveBeenCalledTimes(1)
      const [, options] = mockOpen.mock.calls[0]
      expect(options.data.isEditing).toBe(false)
      expect(options.data.state).toMatchObject({
        name: '',
        username: '',
        email: '',
        password: '',
        status: true,
      })
    })

    it('creates an account and refreshes the list on confirm', async () => {
      mockAccountsPage(() => makePage([], 0))
      let createBody: CreateAccountRequest | null = null
      let listCallCount = 0
      server.use(
        http.get('*/api/accounts', () => {
          listCallCount++
          return HttpResponse.json(makePage([], 0))
        }),
        http.post('*/api/accounts', async ({ request }) => {
          createBody = (await request.json()) as CreateAccountRequest
          return HttpResponse.json(makeAccount())
        }),
      )

      const { wrapper } = await renderAccountsView()
      expect(listCallCount).toBe(1)

      await wrapper.find('button').trigger('click')
      const [, options] = mockOpen.mock.calls[0]
      options.data.state.name = 'Bob'
      options.data.state.username = 'bob'
      options.data.state.email = 'bob@example.com'
      options.data.state.password = 'secret'
      options.data.state.phone = '  '
      options.data.state.position = ''

      const close = vi.fn()
      await options.onConfirm(close)

      expect(createBody).toMatchObject({
        name: 'Bob',
        username: 'bob',
        email: 'bob@example.com',
        password: 'secret',
        phone: null,
        position: null,
        address: null,
      })
      expect(close).toHaveBeenCalledTimes(1)
      expect(listCallCount).toBe(2)
    })
  })

  describe('edit dialog', () => {
    it('seeds edit state from the account, mapping null fields to empty strings', async () => {
      const account = makeAccount({
        id: '5',
        name: 'Carol',
        username: 'carol',
        email: 'carol@example.com',
        phone: null,
        position: null,
        address: null,
      })
      mockAccountsPage(() => makePage([account], 1))
      const { wrapper } = await renderAccountsView()

      const editButtons = wrapper.findAll('button')
      const editButton = editButtons.find((b) => b.attributes('aria-label') === 'Edit account')
      expect(editButton).toBeDefined()
      await editButton!.trigger('click')

      expect(mockOpen).toHaveBeenCalledTimes(1)
      const [, options] = mockOpen.mock.calls[0]
      expect(options.data.isEditing).toBe(true)
      expect(options.data.state).toMatchObject({
        name: 'Carol',
        username: 'carol',
        email: 'carol@example.com',
        phone: '',
        position: '',
        address: '',
        password: '',
      })
    })

    it('updates the account without a password field and refreshes the list', async () => {
      const account = makeAccount({ id: '5', name: 'Carol' })
      let updateBody: UpdateAccountRequest | null = null
      let listCallCount = 0
      server.use(
        http.get('*/api/accounts', () => {
          listCallCount++
          return HttpResponse.json(makePage([account], 1))
        }),
        http.put('*/api/accounts/5', async ({ request }) => {
          updateBody = (await request.json()) as UpdateAccountRequest
          return HttpResponse.json(account)
        }),
      )

      const { wrapper } = await renderAccountsView()
      expect(listCallCount).toBe(1)

      const editButtons = wrapper.findAll('button')
      const editButton = editButtons.find((b) => b.attributes('aria-label') === 'Edit account')
      await editButton!.trigger('click')

      const [, options] = mockOpen.mock.calls[0]
      const close = vi.fn()
      await options.onConfirm(close)

      expect(updateBody).not.toBeNull()
      expect(updateBody).not.toHaveProperty('password')
      expect(close).toHaveBeenCalledTimes(1)
      expect(listCallCount).toBe(2)
    })
  })

  describe('delete', () => {
    it('deletes the account after confirmation and refreshes the list', async () => {
      const account = makeAccount({ id: '9', name: 'Dave' })
      let deleteCalled = false
      let listCallCount = 0
      server.use(
        http.get('*/api/accounts', () => {
          listCallCount++
          return HttpResponse.json(makePage(deleteCalled ? [] : [account], deleteCalled ? 0 : 1))
        }),
        http.delete('*/api/accounts/9', () => {
          deleteCalled = true
          return new HttpResponse(null, { status: 204 })
        }),
      )

      const { wrapper } = await renderAccountsView()
      expect(listCallCount).toBe(1)

      const deleteButtons = wrapper.findAll('button')
      const deleteButton = deleteButtons.find(
        (b) => b.attributes('aria-label') === 'Are you sure you want to delete this account?',
      )
      expect(deleteButton).toBeDefined()
      await deleteButton!.trigger('click')
      await flushPromises()

      expect(deleteCalled).toBe(true)
      expect(listCallCount).toBe(2)
    })
  })

  describe('formatDate', () => {
    it('renders a placeholder when there is no date', async () => {
      const account = makeAccount({ createdAt: '2026-01-01T00:00:00Z', updatedAt: null })
      account.createdAt = ''
      mockAccountsPage(() => makePage([account], 1))

      const { wrapper } = await renderAccountsView()

      expect(wrapper.text()).toContain('-')
    })

    it('renders a formatted date when one is available', async () => {
      const account = makeAccount({ updatedAt: '2026-06-01T10:00:00Z' })
      mockAccountsPage(() => makePage([account], 1))

      const { wrapper } = await renderAccountsView()

      expect(wrapper.text()).not.toContain('undefined')
      expect(wrapper.text()).toContain('2026')
    })
  })
})
