import type { Locale, Tree } from './sidebar-types'

// Base and fork pages live in separate source trees (src/base/, src/app/) so a fork never edits
// base files; rewrites merge both into one URL space. English pages sit under en/ with the same
// slugs, so VitePress's language switcher lands on the matching page.

export const rewrites = {
  'base/:slug*': ':slug*',
  'app/:slug*': ':slug*',
}

const localePrefix = (locale: Locale) => (locale === 'en' ? 'en/' : '')

export function pageSourcePath(tree: Tree, locale: Locale, chapter: string, item?: string): string {
  const page = item === undefined ? `${chapter}.md` : `${chapter}/${item}.md`
  return `${tree}/${localePrefix(locale)}${page}`
}

export function pageLink(locale: Locale, chapter: string, item?: string): string {
  const page =
    item === undefined ? chapter : item === 'index' ? `${chapter}/` : `${chapter}/${item}`
  return `/${localePrefix(locale)}${page}`
}

export function rewritePath(source: string): string {
  return source.replace(/^(base|app)\//, '')
}

/** Works on source paths (`base/en/…`) and rewritten ones (`en/…`). */
export function localeOf(path: string | undefined): Locale {
  return path !== undefined && rewritePath(path).startsWith('en/') ? 'en' : 'vi'
}
