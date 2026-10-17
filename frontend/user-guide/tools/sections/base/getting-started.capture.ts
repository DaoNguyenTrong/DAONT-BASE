import { locales, test } from '../../fixtures'

// "Bắt đầu" chapter: the main screen with its three areas, numbered as in the page's table.
for (const locale of locales) {
  test(`overview [${locale}]`, async ({ page, capture }) => {
    await page.goto('/')
    await capture.useLocale(locale)
    await capture.annotate([
      { selector: 'aside nav', n: 1, at: 'top-right' },
      { selector: 'header .n-breadcrumb', n: 2, at: 'after' },
      { selector: 'main', n: 3 },
    ])
    await capture.blur(['[data-testid="app-sidebar-account"]'])
    await capture.shot({ locale, chapter: 'getting-started', name: 'overview' })
  })
}
