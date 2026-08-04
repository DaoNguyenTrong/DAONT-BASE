import { describe, it, expect, vi } from 'vitest'
import { http, HttpResponse } from 'msw'
import NotificationBell from '@/components/NotificationBell.vue'
import { renderComponent } from '../helpers/render'
import { server } from '../../tests/helpers/msw/server'

function emptyList() {
  return HttpResponse.json({
    items: [],
    totalCount: 0,
    pageNumber: 1,
    pageSize: 10,
    totalPages: 0,
    hasPreviousPage: false,
    hasNextPage: false,
  })
}

function listWithOne() {
  return HttpResponse.json({
    items: [
      {
        id: 'notif-1',
        type: 'OrganizationMemberAdded',
        data: null,
        isRead: false,
        createdAt: '2026-08-04T00:00:00Z',
      },
    ],
    totalCount: 1,
    pageNumber: 1,
    pageSize: 10,
    totalPages: 1,
    hasPreviousPage: false,
    hasNextPage: false,
  })
}

describe('NotificationBell', () => {
  it('hides the badge count when there are no unread notifications', async () => {
    server.use(http.get('*/api/notifications', emptyList))
    const { wrapper } = await renderComponent(NotificationBell, {
      initialState: { notifications: { unreadCount: 0 } },
      global: { stubs: { teleport: true } },
    })

    expect(wrapper.text()).not.toContain('5')
  })

  it('shows the badge count when there are unread notifications', async () => {
    server.use(http.get('*/api/notifications', emptyList))
    const { wrapper } = await renderComponent(NotificationBell, {
      initialState: { notifications: { unreadCount: 5 } },
      global: { stubs: { teleport: true } },
    })

    expect(wrapper.text()).toContain('5')
  })

  it('shows the empty state when the list has no notifications', async () => {
    server.use(http.get('*/api/notifications', emptyList))
    const { wrapper } = await renderComponent(NotificationBell, {
      initialState: { notifications: { unreadCount: 0 } },
      global: { stubs: { teleport: true } },
    })

    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('No notifications yet.')
  })

  it('renders the translated notification text for a known type', async () => {
    server.use(http.get('*/api/notifications', listWithOne))
    const { wrapper } = await renderComponent(NotificationBell, {
      initialState: { notifications: { unreadCount: 1 } },
      global: { stubs: { teleport: true } },
    })

    await wrapper.find('button').trigger('click')
    await flushPromises()

    expect(wrapper.text()).toContain('You were added to an organization')
  })

  it('marks all as read and calls the endpoint', async () => {
    const hits = vi.fn()
    server.use(
      http.get('*/api/notifications', emptyList),
      http.patch('*/api/notifications/read-all', () => {
        hits()
        return new HttpResponse(null, { status: 204 })
      }),
      http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
    )
    const { wrapper } = await renderComponent(NotificationBell, {
      initialState: { notifications: { unreadCount: 2 } },
      global: { stubs: { teleport: true } },
    })

    await wrapper.find('button').trigger('click')
    await flushPromises()

    const markAllButton = wrapper.findAll('button').find((b) => b.text().includes('Mark all as read'))
    await markAllButton?.trigger('click')
    await flushPromises()

    expect(hits).toHaveBeenCalledTimes(1)
  })
})

function flushPromises() {
  return new Promise((resolve) => setTimeout(resolve, 0))
}
