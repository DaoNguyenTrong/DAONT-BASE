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

  test('the real app stylesheet yields the WeatherPlus values', () => {
    const css = generateBrandTokensCss(appCss)
    expect(css).toContain('--brand-violet-500: #4e3d8f;')
    expect(css).toContain('--brand-violet-600: #5e4c9e;')
    expect(css).toContain('--brand-leaf-400: #91bd44;')
    expect(css).toContain('--brand-surface-950: #121527;')
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
