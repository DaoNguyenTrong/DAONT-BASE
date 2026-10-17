import { expect, test } from 'bun:test'
import { localeOf, pageLink, pageSourcePath, rewritePath } from '../.vitepress/paths'

test('pageSourcePath', () => {
  expect(pageSourcePath('base', 'vi', 'getting-started', 'index')).toBe(
    'base/getting-started/index.md',
  )
  expect(pageSourcePath('base', 'en', 'getting-started', 'sign-in')).toBe(
    'base/en/getting-started/sign-in.md',
  )
  expect(pageSourcePath('app', 'vi', 'faq')).toBe('app/faq.md')
})

test('pageLink', () => {
  expect(pageLink('vi', 'getting-started', 'index')).toBe('/getting-started/')
  expect(pageLink('en', 'getting-started', 'sign-in')).toBe('/en/getting-started/sign-in')
  expect(pageLink('vi', 'faq')).toBe('/faq')
})

test('rewritePath strips only the tree prefix', () => {
  expect(rewritePath('base/en/faq.md')).toBe('en/faq.md')
  expect(rewritePath('app/orders/index.md')).toBe('orders/index.md')
})

test('localeOf accepts source and rewritten paths', () => {
  expect(localeOf('base/en/faq.md')).toBe('en')
  expect(localeOf('en/faq.md')).toBe('en')
  expect(localeOf('app/faq.md')).toBe('vi')
  expect(localeOf(undefined)).toBe('vi')
})
