import { locales, test } from '../../fixtures'

// "Thông báo" chapter: the bell's panel with the seeded unread notification. Nothing is marked read.
// The bell itself carries no mark: the header's buttons sit too close, and interface.md numbers it.
const chapter = 'notifications'
const account = '[data-testid="app-header-user-menu"]'
const bell = 'header .n-badge button'
const panel = '.n-popover:has(.n-list)'

for (const locale of locales) {
  test(`panel [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await page.locator(bell).click()
    await page.locator(`${panel} .n-list-item`).first().waitFor()
    await capture.annotate([
      { selector: `${panel} .bg-primary-500 >> nth=0`, n: 1, at: 'before' },
      { selector: `${panel} .n-list-item button >> nth=0`, n: 2, at: 'below' },
      { selector: `${panel} .border-b button`, n: 3, at: 'before' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'panel' })
  })
}
