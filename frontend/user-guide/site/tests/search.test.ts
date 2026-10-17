import { expect, test } from 'bun:test'
import { processTerm } from '../.vitepress/search'

test('processTerm strips Vietnamese diacritics', () => {
  expect(processTerm('Hà Nội')).toBe('ha noi')
  expect(processTerm('ĐĂNG NHẬP')).toBe('dang nhap')
  expect(processTerm('Thành viên')).toBe('thanh vien')
})

test('processTerm is self-contained', () => {
  // VitePress ships the function as source text; it must not close over module scope.
  const rebuilt = new Function(`return (${processTerm.toString()})`)()
  expect(rebuilt('Tổ chức')).toBe('to chuc')
})
