import { defineConfig } from 'vitepress'
import { filterHomeFrontmatter, type HomeFrontmatter } from './home'
import { markdown } from './markdown'
import { rewrites } from './paths'
import { product } from './product'
import { search } from './search'
import { sidebar, srcExclude } from './sidebar'
import { resolveVersion } from './version'

const version = resolveVersion()

// Served by nginx at /guide/ on the app's origin; changing `base` means changing the nginx
// location and the Dockerfile COPY too.
export default defineConfig({
  title: product.name,
  base: '/guide/',
  srcDir: 'src',
  outDir: 'dist',
  cleanUrls: true,
  lastUpdated: false,
  head: [['link', { rel: 'icon', type: 'image/webp', href: `/guide/brand/${product.brand.mark}` }]],
  rewrites,
  srcExclude,
  markdown,
  // A hidden base chapter is not built: drop the home cards and quick-start steps pointing into it.
  transformPageData(pageData) {
    if (pageData.frontmatter.layout === 'home') {
      pageData.frontmatter = filterHomeFrontmatter(
        pageData.frontmatter as HomeFrontmatter,
        product.hiddenBaseChapters,
      )
    }
  },
  themeConfig: {
    // The brand block (theme slot nav-bar-title-before) replaces the site title.
    siteTitle: false,
    search,
    externalLinkIcon: false,
  },
  locales: {
    root: {
      label: 'Tiếng Việt',
      lang: 'vi-VN',
      themeConfig: {
        sidebar: sidebar.vi,
        outline: { level: [2, 3], label: 'Trên trang này' },
        docFooter: { prev: 'Trang trước', next: 'Trang sau' },
        darkModeSwitchLabel: 'Giao diện',
        lightModeSwitchTitle: 'Chuyển sang giao diện sáng',
        darkModeSwitchTitle: 'Chuyển sang giao diện tối',
        sidebarMenuLabel: 'Mục lục',
        returnToTopLabel: 'Lên đầu trang',
        skipToContentLabel: 'Chuyển đến nội dung',
        langMenuLabel: 'Đổi ngôn ngữ',
        footer: { message: `© ${product.publisher} · Phiên bản ${version}` },
        notFound: {
          title: 'KHÔNG TÌM THẤY TRANG',
          quote:
            'Trang bạn tìm không có trong tài liệu hướng dẫn. Hãy dùng mục lục bên trái hoặc ô tìm kiếm.',
          linkLabel: 'Về trang đầu hướng dẫn',
          linkText: 'Về trang đầu',
        },
      },
    },
    en: {
      label: 'English',
      lang: 'en-US',
      link: '/en/',
      themeConfig: {
        sidebar: sidebar.en,
        outline: { level: [2, 3], label: 'On this page' },
        docFooter: { prev: 'Previous page', next: 'Next page' },
        darkModeSwitchLabel: 'Appearance',
        lightModeSwitchTitle: 'Switch to light theme',
        darkModeSwitchTitle: 'Switch to dark theme',
        sidebarMenuLabel: 'Contents',
        returnToTopLabel: 'Return to top',
        skipToContentLabel: 'Skip to content',
        langMenuLabel: 'Change language',
        footer: { message: `© ${product.publisher} · Version ${version}` },
        notFound: {
          title: 'PAGE NOT FOUND',
          quote:
            "The page you're looking for isn't in this guide. Use the table of contents or search.",
          linkLabel: 'Go to the guide home',
          linkText: 'Guide home',
        },
      },
    },
  },
  vite: {
    define: {
      __GUIDE__: JSON.stringify({ ...product, version }),
    },
  },
})
