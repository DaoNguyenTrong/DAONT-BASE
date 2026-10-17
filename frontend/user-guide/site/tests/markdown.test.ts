import { describe, expect, test } from 'bun:test'
import { createMarkdownRenderer } from 'vitepress'
import { markdown } from '../.vitepress/markdown'

const md = await createMarkdownRenderer('src', markdown, '/guide/')
const render = (src: string, relativePath: string) => md.render(src, { relativePath })

describe('callout titles', () => {
  test('Vietnamese pages get Vietnamese titles', () => {
    expect(render('::: tip\nx\n:::', 'base/getting-started/index.md')).toContain(
      '<p class="custom-block-title">Mẹo</p>',
    )
  })
  test('English pages get English titles', () => {
    expect(render('::: warning\nx\n:::', 'base/en/getting-started/index.md')).toContain(
      '<p class="custom-block-title">Important</p>',
    )
  })
  test('an explicit title is kept', () => {
    expect(render('::: warning Heads up\nx\n:::', 'base/en/faq.md')).toContain(
      '<p class="custom-block-title">Heads up</p>',
    )
  })
})

test('tables are wrapped for horizontal scrolling', () => {
  expect(render('| a |\n|---|\n| b |', 'base/faq.md')).toMatch(
    /<div class="guide-table"><table>[\s\S]*<\/table>\s*<\/div>/,
  )
})

test('heading permalink label uses the rendered title in the page language', () => {
  expect(render('## Đổi `mật khẩu`', 'base/faq.md')).toContain(
    'aria-label="Liên kết tới mục &quot;Đổi mật khẩu&quot;"',
  )
  expect(render('## Sessions', 'base/en/faq.md')).toContain(
    'aria-label="Link to section &quot;Sessions&quot;"',
  )
})

describe('permission note', () => {
  test('is placed right under the page title, once', () => {
    const html = render('# Thêm thành viên\n\nBody\n\n# Second', 'base/organizations/add-member.md')
    expect(html).toMatch(/<\/h1>\s*<PermissionNote \/>\s*<p>Body<\/p>/)
    expect(html.match(/<PermissionNote \/>/g)).toHaveLength(1)
  })
  test('is absent from a page without a title', () => {
    expect(render('Body only', 'base/faq.md')).not.toContain('PermissionNote')
  })
})
