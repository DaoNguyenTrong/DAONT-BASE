import type { MarkdownOptions } from 'vitepress'
import { localeOf } from './paths'

const CONTAINER_TITLES_EN: Record<string, string> = {
  info: 'Note',
  tip: 'Tip',
  warning: 'Important',
  danger: 'Warning',
  details: 'Details',
}

const PERMALINK_LABEL = {
  vi: (title: string) => `Liên kết tới mục "${title}"`,
  en: (title: string) => `Link to section "${title}"`,
}

export const markdown: MarkdownOptions = {
  image: { lazyLoading: true },
  // VitePress has one set of callout labels for the whole site, so these are the Vietnamese ones
  // (the root locale); English pages get theirs from the `guide-container-labels` rule below.
  container: {
    infoLabel: 'Lưu ý',
    tipLabel: 'Mẹo',
    warningLabel: 'Quan trọng',
    dangerLabel: 'Cảnh báo',
    detailsLabel: 'Chi tiết',
  },
  anchor: {
    // Replaces VitePress's permalink, whose "Permalink to …" label is the heading's raw source: the
    // label here is the rendered text (no backticks), in the page's language.
    permalink: (slug: string, _opts: unknown, state: any, idx: number) => {
      const inline = state.tokens[idx + 1]
      const title = inline.children
        .filter((t: any) => t.type === 'text' || t.type === 'code_inline')
        .map((t: any) => t.content)
        .join('')
        .trim()
      const space = new state.Token('text', '', 0)
      space.content = ' '
      const open = new state.Token('link_open', 'a', 1)
      open.attrs = [
        ['class', 'header-anchor'],
        ['href', `#${slug}`],
        ['aria-label', PERMALINK_LABEL[localeOf(state.env?.relativePath)](title)],
      ]
      const symbol = new state.Token('html_inline', '', 0)
      symbol.content = '&ZeroWidthSpace;'
      const close = new state.Token('link_close', 'a', -1)
      inline.children.push(space, open, symbol, close)
    },
  },
  config: (md) => {
    // VitePress makes <table> `display: block` so wide tables scroll, which also shrinks narrow
    // tables to their content; the wrapper scrolls instead and the table stays full width.
    md.renderer.rules.table_open = (tokens, idx, options, _env, self) =>
      `<div class="guide-table">${self.renderToken(tokens, idx, options)}`
    md.renderer.rules.table_close = (tokens, idx, options, _env, self) =>
      `${self.renderToken(tokens, idx, options)}</div>`

    // PermissionNote (a global theme component) goes right under the page title; it renders nothing
    // unless the frontmatter sets `permission:`. No theme slot sits between the title and the body.
    md.core.ruler.push('guide-permission-note', (state) => {
      const close = state.tokens.findIndex((t) => t.type === 'heading_close' && t.tag === 'h1')
      if (close === -1) return
      const note = new state.Token('html_block', '', 0)
      note.content = '<PermissionNote />\n'
      state.tokens.splice(close + 1, 0, note)
    })

    // An untitled callout on an English page gets the English title; the container renderer uses
    // whatever follows the type in `info` as the title.
    md.core.ruler.push('guide-container-labels', (state) => {
      if (localeOf(state.env?.relativePath) !== 'en') return
      for (const token of state.tokens) {
        const type = /^container_(\w+)_open$/.exec(token.type)?.[1]
        if (type && CONTAINER_TITLES_EN[type] && token.info.trim() === type) {
          token.info = `${type} ${CONTAINER_TITLES_EN[type]}`
        }
      }
    })
  },
}
