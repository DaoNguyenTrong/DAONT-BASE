import type { Page } from '@playwright/test'
import { locales, test } from '../../fixtures'

// "Tài khoản" chapter: the Profile dialog, one picture per tab. Nothing is saved.
const chapter = 'account'
const tab = (n: number) => `.n-modal .n-tabs-tab >> nth=${n}`
const field = (n: number) => `.n-modal .n-tab-pane form .n-form-item >> nth=${n}`
const primary = '.n-modal .n-tab-pane .n-button--primary-type'
const account = '[data-testid="app-header-user-menu"]'

async function openProfile(page: Page, tabIndex: number) {
  await page.locator(account).click()
  await page.locator('[data-testid="user-menu-profile"]').click()
  await page.locator('.n-modal .n-tabs').waitFor()
  if (tabIndex > 0) await page.locator(tab(tabIndex)).click()
}

for (const locale of locales) {
  test(`profile [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await openProfile(page, 0)
    await capture.annotate([
      { selector: field(0), n: 1, at: 'top-right' },
      { selector: field(1), n: 2, at: 'top-right' },
      { selector: primary, n: 3, at: 'after' },
    ])
    await capture.blur([
      account,
      '.n-modal .n-tab-pane form .n-form-item:first-child input',
      '.n-modal input[type="email"]',
    ])
    await capture.shot({ locale, chapter, name: 'profile' })
  })

  test(`change-password [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await openProfile(page, 1)
    await capture.annotate([
      { selector: tab(1), n: 1, at: 'below' },
      { selector: field(0), n: 2, at: 'top-right' },
      { selector: field(1), n: 3, at: 'top-right' },
      { selector: primary, n: 4, at: 'after' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'change-password' })
  })

  test(`sessions [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await openProfile(page, 2)
    await page.locator('[data-testid="profile-sessions-list"]').waitFor()
    await capture.annotate([
      { selector: tab(2), n: 1, at: 'below' },
      { selector: '.n-modal .n-tab-pane .n-button--error-type >> nth=0', n: 2, at: 'below' },
      { selector: '[data-testid="profile-sessions-list"] li .n-tag >> nth=0', n: 3, at: 'below' },
      { selector: '[data-testid="profile-sessions-list"] li button >> nth=0', n: 4, at: 'below' },
    ])
    await capture.blur([
      account,
      '[data-testid="profile-sessions-list"] li p.font-medium',
      '[data-testid="profile-sessions-list"] li p.text-xs',
    ])
    await capture.shot({ locale, chapter, name: 'sessions' })
  })
}
