import { expect, test } from 'bun:test'
import { strings } from '../.vitepress/theme/strings'

test('every UI string exists in both languages and is not blank', () => {
  expect(Object.keys(strings.vi).sort()).toEqual(Object.keys(strings.en).sort())
  for (const locale of ['vi', 'en'] as const) {
    for (const value of Object.values(strings[locale])) expect(value.trim()).not.toBe('')
  }
})
