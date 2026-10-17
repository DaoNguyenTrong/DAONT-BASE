// Checks the real guide sources: both languages present, every page reachable from the sidebar,
// images present with alt text, known permission codes, and no link that a fork hiding a base
// chapter would turn into a dead link.
import { describe, expect, test } from 'bun:test'
import { existsSync, readdirSync, readFileSync } from 'node:fs'
import { dirname, join, relative } from 'node:path'
import { Permissions } from '../../../src/lib/permissions'
import en from '../../../src/locales/en'
import vi from '../../../src/locales/vi'
import { rewritePath } from '../.vitepress/paths'
import { chapters, sidebar, srcExclude } from '../.vitepress/sidebar'
import { permissionLabels } from '../.vitepress/theme/permission-labels'
import {
  chapterOfUrl,
  counterpart,
  extractImages,
  extractLinks,
  listPages,
  parseFrontmatter,
  permissionProblems,
  resolveLink,
} from './lib/content'

const srcDir = join(import.meta.dir, '../src')
const excluded = srcExclude.map((pattern) => new Bun.Glob(pattern))
const pages = listPages(srcDir).filter((page) => !excluded.some((glob) => glob.match(page)))
const read = (page: string) => readFileSync(join(srcDir, page), 'utf8')
const urlOf = (page: string) =>
  '/' +
  rewritePath(page)
    .replace(/\.md$/, '')
    .replace(/index$/, '')

const sidebarLinks = (items: { link?: string; items?: { link?: string }[] }[]): string[] =>
  items.flatMap((item) => [
    ...(item.link ? [item.link] : []),
    ...sidebarLinks((item.items ?? []) as never),
  ])

describe('content integrity', () => {
  test('every page exists in both languages', () => {
    const missing = pages.filter((page) => !existsSync(join(srcDir, counterpart(page))))
    expect(missing).toEqual([])
  })

  test('every page is in the sidebar and every sidebar link has both files', () => {
    const pageUrls = [...new Set(pages.map(urlOf))].sort()
    const linked = [...new Set([...sidebarLinks(sidebar.vi), ...sidebarLinks(sidebar.en)])].sort()
    expect({ notInSidebar: pageUrls.filter((u) => !linked.includes(u)) }).toEqual({
      notInSidebar: [],
    })
    expect({ noPage: linked.filter((u) => !pageUrls.includes(u)) }).toEqual({ noPage: [] })
  })

  test('every page has a title and a description', () => {
    const incomplete = pages.filter((page) => {
      const data = parseFrontmatter(read(page))
      return !data.title?.trim() || !data.description?.trim()
    })
    expect(incomplete).toEqual([])
  })

  test('every image exists and has alt text', () => {
    const problems: string[] = []
    for (const page of pages) {
      for (const { alt, src } of extractImages(read(page))) {
        const file = src.startsWith('/') ? join(srcDir, src) : join(srcDir, dirname(page), src)
        if (!existsSync(file)) problems.push(`${page}: missing ${src}`)
        if (!alt.trim()) problems.push(`${page}: no alt text for ${src}`)
      }
    }
    expect(problems).toEqual([])
  })

  test('every permission a page requires is a known code with a label', () => {
    const pageCodes = pages
      .map((page) => [page, parseFrontmatter(read(page)).permission] as const)
      .filter((entry): entry is readonly [string, string] => entry[1] !== undefined)
    expect(permissionProblems(Object.values(Permissions), permissionLabels, pageCodes)).toEqual([])
  })

  test('a base permission is named as the app names it', () => {
    // The app labels Permissions.X with organizations.permissionX (OrganizationRolesDialog, RoleForm).
    const app = { vi: vi.organizations, en: en.organizations } as const
    const mismatches: string[] = []
    for (const [key, code] of Object.entries(Permissions)) {
      const localeKey = `permission${key}` as keyof typeof en.organizations
      for (const locale of ['vi', 'en'] as const) {
        const expected = app[locale][localeKey]
        if (permissionLabels[code]?.[locale] !== expected)
          mismatches.push(`${code} [${locale}]: ${permissionLabels[code]?.[locale]} ≠ ${expected}`)
      }
    }
    expect(mismatches).toEqual([])
  })

  test('no two pages share a URL', () => {
    const urls = pages.map(urlOf)
    expect(urls.filter((url, i) => urls.indexOf(url) !== i)).toEqual([])
  })

  test('no cross-chapter link into a hideable chapter', () => {
    const hideable = new Set(chapters.filter((c) => c.hideable).map((c) => c.slug))
    const offending: string[] = []
    for (const page of pages) {
      const from = chapterOfUrl(urlOf(page))
      for (const link of extractLinks(read(page))) {
        const to = chapterOfUrl(resolveLink(page, link))
        if (to !== from && hideable.has(to)) offending.push(`${page} → ${link}`)
      }
    }
    expect(offending).toEqual([])
  })
})

// An image nobody references is only a warning: capture runs may produce shots ahead of the page.
const referenced = new Set(
  pages.flatMap((page) =>
    extractImages(read(page)).map(({ src }) =>
      src.startsWith('/') ? join(srcDir, src) : join(srcDir, dirname(page), src),
    ),
  ),
)
for (const tree of ['base', 'app']) {
  const imagesDir = join(srcDir, tree, 'images')
  if (!existsSync(imagesDir)) continue
  for (const entry of readdirSync(imagesDir, { recursive: true, withFileTypes: true })) {
    const file = join(entry.parentPath, entry.name)
    if (entry.isFile() && !referenced.has(file)) {
      console.warn(`unused image: ${relative(srcDir, file)}`)
    }
  }
}
