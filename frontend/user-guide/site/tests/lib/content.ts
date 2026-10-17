// Helpers for tests/content-integrity.test.ts: read the guide's markdown sources the way VitePress
// will serve them (rewrites strip the base/ and app/ prefixes).
import { readdirSync } from 'node:fs'
import { join, relative } from 'node:path'
import { rewritePath } from '../../.vitepress/paths'

export function listPages(srcDir: string): string[] {
  const pages: string[] = []
  const walk = (dir: string) => {
    for (const entry of readdirSync(dir, { withFileTypes: true })) {
      const path = join(dir, entry.name)
      if (entry.isDirectory()) {
        if (entry.name !== 'images') walk(path)
      } else if (entry.name.endsWith('.md')) {
        pages.push(relative(srcDir, path))
      }
    }
  }
  for (const tree of ['base', 'app']) {
    try {
      walk(join(srcDir, tree))
    } catch (error) {
      if ((error as NodeJS.ErrnoException).code !== 'ENOENT') throw error
    }
  }
  return pages.sort()
}

export function counterpart(page: string): string {
  const match = /^(base|app)\/(en\/)?(.*)$/.exec(page)
  if (!match) throw new Error(`Not a guide page: ${page}`)
  const [, tree, en, rest] = match
  return en ? `${tree}/${rest}` : `${tree}/en/${rest}`
}

export function parseFrontmatter(source: string): Record<string, string> {
  const match = /^---\r?\n([\s\S]*?)\r?\n---/.exec(source)
  if (!match) return {}
  const data: Record<string, string> = {}
  for (const line of match[1].split(/\r?\n/)) {
    const field = /^([A-Za-z][\w-]*):\s*(.*)$/.exec(line)
    if (field) data[field[1]] = field[2].trim().replace(/^(['"])(.*)\1$/, '$2')
  }
  return data
}

// What VitePress renders as content: HTML comments and fenced code blocks hold examples, not links.
function renderedText(source: string): string {
  return source.replace(/<!--[\s\S]*?-->/g, '').replace(/^(```|~~~)[\s\S]*?^\1/gm, '')
}

export function extractImages(source: string): { alt: string; src: string }[] {
  return [...renderedText(source).matchAll(/!\[([^\]]*)\]\(([^)\s]+)[^)]*\)/g)].map((m) => ({
    alt: m[1],
    src: m[2],
  }))
}

export function extractLinks(source: string): string[] {
  return [...renderedText(source).matchAll(/(?<!!)\[[^\]]*\]\(([^)\s]+)[^)]*\)/g)]
    .map((m) => m[1])
    .filter((link) => !/^([a-z][a-z0-9+.-]*:|\/\/)/i.test(link))
    .map((link) => link.split('#')[0])
    .filter((link) => link !== '')
}

/** The URL a link on `page` points to, as served: `/en/organizations/add-member`, `/faq/`. */
export function resolveLink(page: string, link: string): string {
  const url = new URL(link, `http://guide/${rewritePath(page)}`)
  return url.pathname.replace(/\.(md|html)$/, '')
}

export function chapterOfUrl(url: string): string {
  return url.replace(/^\/(en\/)?/, '').split('/')[0]
}

/**
 * Permission codes as the guide uses them. The app may define codes no page mentions (a fork adds
 * its own without touching the guide); only codes that pages require must be known and labelled.
 */
export function permissionProblems(
  codes: string[],
  labels: Record<string, unknown>,
  pageCodes: (readonly [string, string])[],
): string[] {
  return [
    ...pageCodes.flatMap(([page, code]) =>
      !codes.includes(code)
        ? [`${page}: unknown permission "${code}"`]
        : labels[code] === undefined
          ? [`${page}: no label for "${code}"`]
          : [],
    ),
    ...Object.keys(labels)
      .filter((code) => !codes.includes(code))
      .map((code) => `label for unknown permission "${code}"`),
  ]
}
