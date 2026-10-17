import { describe, expect, test } from 'bun:test'
import {
  chapterOfUrl,
  counterpart,
  extractImages,
  extractLinks,
  parseFrontmatter,
  permissionProblems,
  resolveLink,
} from './lib/content'

test('counterpart', () => {
  expect(counterpart('base/faq.md')).toBe('base/en/faq.md')
  expect(counterpart('app/en/orders/index.md')).toBe('app/orders/index.md')
})

test('parseFrontmatter', () => {
  expect(
    parseFrontmatter(
      '---\ntitle: "Thêm thành viên"\npermission: organizations.members.manage\n---\n# x',
    ),
  ).toEqual({ title: 'Thêm thành viên', permission: 'organizations.members.manage' })
  expect(parseFrontmatter('# no frontmatter')).toEqual({})
})

test('parseFrontmatter ignores commented lines', () => {
  expect(parseFrontmatter("---\ntitle: 'a: b'\n# permission: x\n---\n")).toEqual({ title: 'a: b' })
})

test('extractImages and extractLinks', () => {
  const src =
    '![Màn hình](/base/images/vi/a/b.jpg) [x](../organizations/add-member#step) [y](https://e.com) [z](#top)'
  expect(extractImages(src)).toEqual([{ alt: 'Màn hình', src: '/base/images/vi/a/b.jpg' }])
  expect(extractLinks(src)).toEqual(['../organizations/add-member'])
})

test('resolveLink uses the rewritten location', () => {
  expect(resolveLink('base/en/getting-started/index.md', '../organizations/add-member')).toBe(
    '/en/organizations/add-member',
  )
  expect(resolveLink('base/faq.md', './getting-started/')).toBe('/getting-started/')
  expect(resolveLink('base/faq.md', '/en/faq')).toBe('/en/faq')
  expect(resolveLink('base/getting-started/index.md', '../faq.md')).toBe('/faq')
})

test('chapterOfUrl', () => {
  expect(chapterOfUrl('/en/organizations/add-member')).toBe('organizations')
  expect(chapterOfUrl('/faq')).toBe('faq')
})

test('extractors skip HTML comments and fenced code, as VitePress renders neither as links', () => {
  const src = [
    '<!-- ![alt](/base/images/vi/<chương>/<tên>.jpg) [x](../a) -->',
    '```md',
    '![shown](/nowhere.jpg) [y](../b)',
    '```',
    '![real](/base/images/vi/a.jpg) [z](../c)',
  ].join('\n')
  expect(extractImages(src)).toEqual([{ alt: 'real', src: '/base/images/vi/a.jpg' }])
  expect(extractLinks(src)).toEqual(['../c'])
})

test('the page template yields no image or link to check', async () => {
  const template = await Bun.file(new URL('../src/_template.md', import.meta.url)).text()
  expect(extractImages(template)).toEqual([])
  expect(extractLinks(template)).toEqual([])
})

describe('permissionProblems', () => {
  const labels = { 'a.manage': { vi: 'A', en: 'A' } }
  test('an app permission with no page and no label is fine', () => {
    expect(permissionProblems(['a.manage', 'orders.manage'], labels, [])).toEqual([])
  })
  test('a page needing an unknown or unlabelled code is reported', () => {
    expect(
      permissionProblems(['a.manage', 'orders.manage'], labels, [
        ['base/x.md', 'nope'],
        ['app/y.md', 'orders.manage'],
      ]),
    ).toEqual(['base/x.md: unknown permission "nope"', 'app/y.md: no label for "orders.manage"'])
  })
  test('a label for a code the app no longer has is reported', () => {
    expect(permissionProblems([], labels, [])).toEqual(['label for unknown permission "a.manage"'])
  })
})
