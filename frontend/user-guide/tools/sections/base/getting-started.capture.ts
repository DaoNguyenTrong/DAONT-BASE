import { locales, test } from '../../fixtures'

// "Bắt đầu" chapter. Numbers match each page's "# / Thành phần" table.
const chapter = 'getting-started'
const account = '[data-testid="app-header-user-menu"]'

for (const locale of locales) {
  test(`overview [${locale}]`, async ({ capture }) => {
    await capture.open(locale)
    await capture.annotate([
      { selector: '[data-testid="app-sidebar"] nav', n: 1, at: 'top-right' },
      { selector: '[data-testid="app-header-title"]', n: 2, at: 'after' },
      { selector: 'main', n: 3 },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'overview' })
  })

  test(`register [${locale}]`, async ({ guest: { capture } }) => {
    await capture.open(locale, '/#/register', 'form .n-form-item >> nth=3')
    await capture.annotate([
      { selector: 'form .n-form-item >> nth=0', n: 1, at: 'after' },
      { selector: 'form .n-form-item >> nth=1', n: 2, at: 'after' },
      { selector: 'form .n-form-item >> nth=2', n: 3, at: 'after' },
      { selector: 'form .n-form-item >> nth=3', n: 4, at: 'after' },
      { selector: 'form button[type="submit"]', n: 5, at: 'after' },
      { selector: 'button:has-text("Google")', n: 6, at: 'after' },
    ])
    await capture.shot({ locale, chapter, name: 'register' })
  })

  test(`sign-in [${locale}]`, async ({ guest: { capture } }) => {
    await capture.open(locale, '/#/login', '#username')
    await capture.annotate([
      { selector: '#username', n: 1, at: 'after' },
      { selector: '#password', n: 2, at: 'after' },
      { selector: 'form .n-checkbox', n: 3, at: 'after' },
      { selector: 'form button[type="submit"]', n: 4, at: 'after' },
      { selector: 'a[href="#/register"]', n: 5, at: 'after' },
      { selector: 'button:has-text("Google")', n: 6, at: 'after' },
    ])
    await capture.shot({ locale, chapter, name: 'sign-in' })
  })

  // The dialog is only opened for the picture; Confirm is never clicked.
  test(`sign-out [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await page.locator(account).click()
    await page.locator('[data-testid="user-menu-logout"]').click()
    await page.locator('.n-dialog').waitFor()
    await capture.annotate([
      { selector: account, n: 1, at: 'below' },
      { selector: '.n-dialog__action button >> nth=1', n: 2, at: 'below' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'sign-out' })
  })

  test(`interface-controls [${locale}]`, async ({ capture }) => {
    await capture.open(locale)
    await capture.annotate([
      { selector: '[data-testid="app-header-toggle"]', n: 1, at: 'below' },
      { selector: '[data-testid="app-header-title"]', n: 2, at: 'after' },
      { selector: '[data-testid="app-header-palette"]', n: 3, at: 'below' },
      { selector: '[data-testid="app-header-user-guide"]', n: 4, at: 'below' },
      { selector: 'header .n-badge', n: 5, at: 'below' },
      { selector: '[data-testid="app-header-settings"]', n: 6, at: 'below' },
      { selector: account, n: 7, at: 'below' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'interface-controls', clip: 'header', pad: 48 })
  })
}
