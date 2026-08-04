import { http, HttpResponse } from 'msw'

export const handlers = [
  http.get('*/api/health', () =>
    HttpResponse.json({
      status: 'healthy',
      version: '1.2.0',
      timestamp: '2026-06-01T10:00:00Z',
    }),
  ),
  http.get('*/api/auth/sessions', () => HttpResponse.json([])),
  http.get('*/api/notifications/unread-count', () => HttpResponse.json({ count: 0 })),
]
