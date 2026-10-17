// `bun run new-page <chapter>[/<page>] [--app]`: creates the Vietnamese and English pages of a new
// guide page from src/_template.md, so neither language is forgotten, then prints the sidebar entry
// to add by hand (the script never edits TypeScript).
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { pageSourcePath } from '../.vitepress/paths'
import type { Tree } from '../.vitepress/sidebar-types'

const SLUG = /^[a-z0-9]+(-[a-z0-9]+)*$/

export function parseTarget(target: string): { chapter: string; item?: string } {
  const parts = target.split('/')
  if (parts.length > 2 || !parts.every((part) => SLUG.test(part))) {
    throw new Error(`Invalid page path "${target}": use kebab-case "chapter" or "chapter/page"`)
  }
  return parts.length === 2 ? { chapter: parts[0], item: parts[1] } : { chapter: parts[0] }
}

export function createPage(opts: { srcDir: string; target: string; tree: Tree }): {
  created: string[]
  sidebarSnippet: string
} {
  const { chapter, item } = parseTarget(opts.target)
  const targets = (['vi', 'en'] as const).map((locale) =>
    pageSourcePath(opts.tree, locale, chapter, item),
  )
  const existing = targets.find((page) => existsSync(join(opts.srcDir, page)))
  if (existing) throw new Error(`Page already exists: ${existing}`)

  const template = readFileSync(join(opts.srcDir, '_template.md'), 'utf8')
  for (const page of targets) {
    mkdirSync(dirname(join(opts.srcDir, page)), { recursive: true })
    writeFileSync(join(opts.srcDir, page), template)
  }
  return {
    created: targets,
    sidebarSnippet: `{ slug: '${item ?? chapter}', text: { vi: '', en: '' } },`,
  }
}

if (import.meta.main) {
  const args = process.argv.slice(2)
  const tree: Tree = args.includes('--app') ? 'app' : 'base'
  const target = args.find((arg) => !arg.startsWith('--')) ?? ''
  try {
    const { created, sidebarSnippet } = createPage({
      srcDir: resolve(import.meta.dir, '../src'),
      target,
      tree,
    })
    console.log(`Created:\n${created.map((page) => `  src/${page}`).join('\n')}`)
    const where = target.includes('/')
      ? `the items of chapter "${target.split('/')[0]}"`
      : 'the chapter list'
    console.log(`\nAdd to ${where} in .vitepress/sidebar.${tree}.ts:\n  ${sidebarSnippet}`)
  } catch (error) {
    console.error((error as Error).message)
    process.exit(1)
  }
}
