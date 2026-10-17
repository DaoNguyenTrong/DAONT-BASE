import { beforeEach, describe, expect, test } from 'bun:test'
import { existsSync, mkdirSync, mkdtempSync, readFileSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { createPage, parseTarget } from '../scripts/new-page'

let srcDir: string
beforeEach(() => {
  srcDir = mkdtempSync(join(tmpdir(), 'guide-src-'))
  writeFileSync(join(srcDir, '_template.md'), '---\ntitle: <t>\n---\n')
})

describe('parseTarget', () => {
  test('accepts chapter and chapter/page', () => {
    expect(parseTarget('organizations/add-member')).toEqual({
      chapter: 'organizations',
      item: 'add-member',
    })
    expect(parseTarget('faq')).toEqual({ chapter: 'faq' })
  })
  test.each(['Organizations/x', 'tổ-chức', 'a/b/c', 'a//b', '', 'a_b'])('rejects %p', (bad) => {
    expect(() => parseTarget(bad)).toThrow('Invalid page path')
  })
})

describe('createPage', () => {
  test('writes both languages from the template', () => {
    const { created, sidebarSnippet } = createPage({ srcDir, target: 'orders/create', tree: 'app' })
    expect(created).toEqual(['app/orders/create.md', 'app/en/orders/create.md'])
    expect(readFileSync(join(srcDir, 'app/en/orders/create.md'), 'utf8')).toBe(
      '---\ntitle: <t>\n---\n',
    )
    expect(sidebarSnippet).toBe("{ slug: 'create', text: { vi: '', en: '' } },")
  })
  test('refuses when only one language already exists and writes nothing', () => {
    mkdirSync(join(srcDir, 'base/en'), { recursive: true })
    writeFileSync(join(srcDir, 'base/en/faq.md'), 'mine')
    expect(() => createPage({ srcDir, target: 'faq', tree: 'base' })).toThrow(
      'Page already exists: base/en/faq.md',
    )
    expect(existsSync(join(srcDir, 'base/faq.md'))).toBe(false)
    expect(readFileSync(join(srcDir, 'base/en/faq.md'), 'utf8')).toBe('mine')
  })
})
