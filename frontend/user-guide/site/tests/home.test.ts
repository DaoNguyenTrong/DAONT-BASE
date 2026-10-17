// The home page: its feature cards and "Quick start" steps come from each index.md's frontmatter, and
// a fork that hides a base chapter must not keep a card or a step pointing into it.
import { describe, expect, test } from 'bun:test'
import { existsSync, readFileSync } from 'node:fs'
import { join } from 'node:path'
import { filterHomeFrontmatter, type HomeFrontmatter } from '../.vitepress/home'

const srcDir = join(import.meta.dir, '../src')

function homeOf(page: string): HomeFrontmatter {
  const source = readFileSync(join(srcDir, page), 'utf8')
  const yaml = source.match(/^---\n([\s\S]*?)\n---/)?.[1] ?? ''
  return Bun.YAML.parse(yaml) as HomeFrontmatter
}

const card = (link: string) => ({ title: link, details: '', link, linkText: '' })
const step = (link: string) => ({ title: link, details: '', link, image: '' })

describe('filterHomeFrontmatter', () => {
  test('drops the cards and steps that point into a hidden chapter, in both languages', () => {
    const home: HomeFrontmatter = {
      layout: 'home',
      features: [card('/getting-started/sign-in'), card('/organizations/switch')],
      quickStart: {
        title: '',
        steps: [step('/en/getting-started/sign-in'), step('/en/organizations/switch')],
      },
    }

    const filtered = filterHomeFrontmatter(home, ['organizations'])

    expect(filtered.features?.map((f) => f.link)).toEqual(['/getting-started/sign-in'])
    expect(filtered.quickStart?.steps.map((s) => s.link)).toEqual(['/en/getting-started/sign-in'])
  })

  test('matches a single-page chapter by its slug only', () => {
    const home: HomeFrontmatter = {
      features: [card('/notifications'), card('/notifications-archive/x')],
    }

    expect(filterHomeFrontmatter(home, ['notifications']).features?.map((f) => f.link)).toEqual([
      '/notifications-archive/x',
    ])
  })

  test('leaves the frontmatter alone when nothing is hidden', () => {
    const home: HomeFrontmatter = { features: [card('/account/profile')] }

    expect(filterHomeFrontmatter(home, [])).toEqual(home)
  })
})

describe.each([
  ['index.md', ''],
  ['en/index.md', '/en'],
])('%s', (page, prefix) => {
  const home = homeOf(page)
  const pageExists = (link: string) => {
    const path = link.slice(prefix.length) || '/'
    const file = path.endsWith('/') ? `${path}index.md` : `${path}.md`
    return [`base${prefix}${file}`, `app${prefix}${file}`].some((f) => existsSync(join(srcDir, f)))
  }

  test('has six feature cards, each with an icon, a link to a page and link text', () => {
    expect(home.features).toHaveLength(6)
    for (const feature of home.features ?? []) {
      expect(feature.icon).toContain('<svg')
      expect(feature.link.startsWith(`${prefix}/`)).toBe(true)
      expect(pageExists(feature.link)).toBe(true)
      expect(feature.linkText?.trim()).toBeTruthy()
    }
  })

  test('has three quick-start steps, each with a link to a page and an existing thumbnail', () => {
    expect(home.quickStart?.title.trim()).toBeTruthy()
    expect(home.quickStart?.steps).toHaveLength(3)
    for (const s of home.quickStart?.steps ?? []) {
      expect(s.title.trim()).toBeTruthy()
      expect(pageExists(s.link)).toBe(true)
      expect(s.image).toMatch(new RegExp(`^/(base|app)/images/${prefix ? 'en' : 'vi'}/`))
      expect(existsSync(join(srcDir, s.image))).toBe(true)
    }
  })
})

test('the vi and en home pages point at the same pages', () => {
  const vi = homeOf('index.md')
  const en = homeOf('en/index.md')
  const strip = (link: string) => link.replace(/^\/en/, '')
  expect(en.features?.map((f) => strip(f.link))).toEqual(vi.features?.map((f) => f.link))
  expect(en.quickStart?.steps.map((s) => strip(s.link))).toEqual(
    vi.quickStart?.steps.map((s) => s.link),
  )
})

test('HomeQuickStart holds no copy of its own', () => {
  const source = readFileSync(
    join(import.meta.dir, '../.vitepress/theme/components/HomeQuickStart.vue'),
    'utf8',
  )
  const template = source.match(/<template>([\s\S]*)<\/template>/)?.[1] ?? ''
  const text = template
    .replace(/<[^>]*>/g, ' ')
    .replace(/\{\{[\s\S]*?\}\}/g, ' ')
    .trim()
  expect(text).toBe('')
})
