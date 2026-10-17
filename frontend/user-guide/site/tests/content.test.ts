import { expect, test } from 'bun:test'
import {
  chapterOfUrl,
  counterpart,
  extractImages,
  extractLinks,
  parseFrontmatter,
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
