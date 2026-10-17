import type { DefaultTheme } from 'vitepress'
import { pageLink } from './paths'
import { product } from './product'
import { appChapters } from './sidebar.app'
import { baseChapters } from './sidebar.base'
import type { Chapter, Locale, ResolvedChapter } from './sidebar-types'

export function resolveChapters(
  base: Chapter[],
  app: Chapter[],
  hidden: string[],
): ResolvedChapter[] {
  for (const slug of hidden) {
    const chapter = base.find((c) => c.slug === slug)
    if (!chapter) throw new Error(`Unknown base chapter "${slug}" in hiddenBaseChapters`)
    if (chapter.hideable !== true) throw new Error(`Base chapter "${slug}" is not hideable`)
  }
  // new-page prints entries with empty labels to fill in; one left blank would ship an empty menu row.
  for (const chapter of [...base, ...app]) {
    const entries = [
      [chapter.slug, chapter.text] as const,
      ...(chapter.items ?? []).map((item) => [`${chapter.slug}/${item.slug}`, item.text] as const),
    ]
    for (const [path, text] of entries) {
      for (const locale of ['vi', 'en'] as const) {
        if (!text[locale].trim())
          throw new Error(`Blank ${locale} label for "${path}" in the sidebar`)
      }
    }
  }
  const duplicate = app.find((a) => base.some((b) => b.slug === a.slug))
  if (duplicate) throw new Error(`Duplicate chapter slug "${duplicate.slug}"`)
  return [
    ...base.filter((c) => !hidden.includes(c.slug)).map((c) => ({ ...c, tree: 'base' as const })),
    ...app.map((c) => ({ ...c, tree: 'app' as const })),
  ]
}

export function buildSidebar(
  chapters: ResolvedChapter[],
  locale: Locale,
): DefaultTheme.SidebarItem[] {
  return chapters.map((chapter) =>
    chapter.items === undefined
      ? { text: chapter.text[locale], link: pageLink(locale, chapter.slug) }
      : {
          text: chapter.text[locale],
          collapsed: true,
          items: chapter.items.map((item) => ({
            text: item.text[locale],
            link: pageLink(locale, chapter.slug, item.slug),
          })),
        },
  )
}

/** srcExclude globs for hidden base chapters: they are neither built nor indexed for search. */
export function hiddenSourceGlobs(base: Chapter[], hidden: string[]): string[] {
  return base
    .filter((c) => hidden.includes(c.slug))
    .flatMap((c) =>
      c.items === undefined
        ? [`base/${c.slug}.md`, `base/en/${c.slug}.md`]
        : [`base/${c.slug}/**`, `base/en/${c.slug}/**`],
    )
}

export const chapters = resolveChapters(baseChapters, appChapters, product.hiddenBaseChapters)

export const sidebar = { vi: buildSidebar(chapters, 'vi'), en: buildSidebar(chapters, 'en') }

export const srcExclude = [
  '_template.md',
  ...hiddenSourceGlobs(baseChapters, product.hiddenBaseChapters),
]
