import type { Chapter } from './sidebar-types'

// [base] The base's chapters. Forks don't edit this file: they add chapters in sidebar.app.ts and
// hide hideable base chapters with product.hiddenBaseChapters.
export const baseChapters: Chapter[] = [
  {
    slug: 'getting-started',
    text: { vi: 'Bắt đầu', en: 'Getting started' },
    items: [{ slug: 'index', text: { vi: 'Giới thiệu', en: 'Introduction' } }],
  },
  { slug: 'faq', text: { vi: 'Câu hỏi thường gặp', en: 'FAQ' } },
]
