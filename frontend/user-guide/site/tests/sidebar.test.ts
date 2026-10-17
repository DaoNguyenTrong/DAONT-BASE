import { describe, expect, test } from 'bun:test'
import type { Chapter } from '../.vitepress/sidebar-types'
import { buildSidebar, hiddenSourceGlobs, resolveChapters } from '../.vitepress/sidebar'

const t = (vi: string, en: string) => ({ vi, en })
const base: Chapter[] = [
  {
    slug: 'getting-started',
    text: t('Bắt đầu', 'Getting started'),
    items: [{ slug: 'index', text: t('Giới thiệu', 'Introduction') }],
  },
  {
    slug: 'organizations',
    text: t('Tổ chức', 'Organizations'),
    hideable: true,
    items: [{ slug: 'add-member', text: t('Thêm thành viên', 'Add a member') }],
  },
  { slug: 'faq', text: t('Câu hỏi thường gặp', 'FAQ') },
]
const app: Chapter[] = [
  {
    slug: 'orders',
    text: t('Đơn hàng', 'Orders'),
    items: [{ slug: 'index', text: t('Tổng quan', 'Overview') }],
  },
]

describe('resolveChapters', () => {
  test('appends app chapters after base and tags the tree', () => {
    expect(resolveChapters(base, app, []).map((c) => `${c.tree}:${c.slug}`)).toEqual([
      'base:getting-started',
      'base:organizations',
      'base:faq',
      'app:orders',
    ])
  })
  test('drops a hidden hideable chapter', () => {
    expect(resolveChapters(base, app, ['organizations']).map((c) => c.slug)).not.toContain(
      'organizations',
    )
  })
  test('rejects hiding a non-hideable chapter', () => {
    expect(() => resolveChapters(base, app, ['faq'])).toThrow('Base chapter "faq" is not hideable')
  })
  test('rejects an unknown hidden slug', () => {
    expect(() => resolveChapters(base, app, ['nope'])).toThrow(
      'Unknown base chapter "nope" in hiddenBaseChapters',
    )
  })
  test('rejects a blank label in either language', () => {
    const blankItem: Chapter[] = [
      {
        slug: 'orders',
        text: t('Đơn hàng', 'Orders'),
        items: [{ slug: 'create', text: t('', '') }],
      },
    ]
    expect(() => resolveChapters(base, blankItem, [])).toThrow(
      'Blank vi label for "orders/create" in the sidebar',
    )
    expect(() => resolveChapters(base, [{ slug: 'x', text: t('X', ' ') }], [])).toThrow(
      'Blank en label for "x" in the sidebar',
    )
  })
  test('rejects a slug used by both trees', () => {
    expect(() => resolveChapters(base, [{ slug: 'faq', text: t('a', 'b') }], [])).toThrow(
      'Duplicate chapter slug "faq"',
    )
  })
})

describe('buildSidebar', () => {
  test('builds the same shape in both locales with locale links', () => {
    const chapters = resolveChapters(base, app, [])
    const vi = buildSidebar(chapters, 'vi')
    const en = buildSidebar(chapters, 'en')
    expect(vi[0]).toEqual({
      text: 'Bắt đầu',
      collapsed: true,
      items: [{ text: 'Giới thiệu', link: '/getting-started/' }],
    })
    expect(en[1].items?.[0]).toEqual({ text: 'Add a member', link: '/en/organizations/add-member' })
    expect(vi[2]).toEqual({ text: 'Câu hỏi thường gặp', link: '/faq' })
    expect(en.length).toBe(vi.length)
  })
})

describe('hiddenSourceGlobs', () => {
  test('covers both locales of a hidden chapter and nothing else', () => {
    const globs = hiddenSourceGlobs(base, ['organizations']).map((g) => new Bun.Glob(g))
    const hit = (p: string) => globs.some((g) => g.match(p))
    expect(hit('base/organizations/add-member.md')).toBe(true)
    expect(hit('base/en/organizations/add-member.md')).toBe(true)
    expect(hit('base/getting-started/index.md')).toBe(false)
    expect(hit('app/organizations/x.md')).toBe(false)
  })
  test('covers a hidden single-page chapter', () => {
    const single: Chapter[] = [{ slug: 'billing', text: t('a', 'b'), hideable: true }]
    expect(hiddenSourceGlobs(single, ['billing'])).toEqual([
      'base/billing.md',
      'base/en/billing.md',
    ])
  })
})
