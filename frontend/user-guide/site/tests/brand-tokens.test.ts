import { describe, expect, test } from 'bun:test'
import { existsSync, mkdtempSync, writeFileSync } from 'node:fs'
import { tmpdir } from 'node:os'
import { join } from 'node:path'
import { generateBrandTokensCss } from '../scripts/brand-tokens'
import { copyLogos } from '../scripts/sync-brand'

const fixture = await Bun.file(new URL('./fixtures/tailwind.css', import.meta.url)).text()
const appCss = await Bun.file(
  new URL('../../../src/assets/styles/tailwind.css', import.meta.url),
).text()

describe('generateBrandTokensCss', () => {
  test('maps every accent and leaf step', () => {
    const css = generateBrandTokensCss(fixture)
    for (const n of [50, 100, 200, 300, 400, 500, 600, 700, 800, 900, 950]) {
      expect(css).toContain(`--brand-violet-${n}:`)
      expect(css).toContain(`--brand-leaf-${n}:`)
    }
  })

  test('resolves chained var() to a hex value', () => {
    expect(generateBrandTokensCss(fixture)).toContain('--brand-violet-700: #123456;')
  })

  test('takes dark-only tokens from the .dark block', () => {
    const css = generateBrandTokensCss(fixture)
    expect(css).toContain('--brand-primary-200-dark: #9c80dc;')
    expect(css).toContain('--brand-surface-900: #1e2235;')
    expect(css).toContain('--brand-surface-950: #121527;')
  })

  test('names every missing token', () => {
    const broken = fixture
      .replace(/--color-leaf-400:[^;]+;/, '')
      .replace(/--color-accent-950:[^;]+;/, '')
    expect(() => generateBrandTokensCss(broken)).toThrow(
      /--color-accent-950.*--color-leaf-400|--color-leaf-400.*--color-accent-950/,
    )
  })

  // Only the shape, never the values: a fork recolours the app without touching this base file.
  test('the real app stylesheet yields every brand token', () => {
    const css = generateBrandTokensCss(appCss)
    const names = [...css.matchAll(/--brand-([\w-]+):/g)].map((m) => m[1])
    expect(names).toHaveLength(25)
  })

  test('a fork that recolours the app still gets every brand token', () => {
    const recoloured = appCss.replace(/#[0-9a-f]{6}\b/gi, '#010203')
    expect(generateBrandTokensCss(recoloured)).toContain('--brand-violet-500: #010203;')
  })
})

describe('copyLogos', () => {
  test('refuses to copy anything when a source file is missing', () => {
    const from = mkdtempSync(join(tmpdir(), 'logos-'))
    const to = mkdtempSync(join(tmpdir(), 'brand-'))
    writeFileSync(join(from, 'light.webp'), 'x')
    const brand = { wordmarkLight: 'light.webp', wordmarkDark: 'dark.webp', mark: 'mark.webp' }
    expect(() => copyLogos(brand, from, to)).toThrow(/dark\.webp.*mark\.webp/)
    expect(existsSync(join(to, 'light.webp'))).toBe(false)
  })
})
