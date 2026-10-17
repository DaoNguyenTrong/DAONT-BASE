import type { Page } from '@playwright/test'
import { locales, test } from '../../fixtures'

// "Tổ chức" chapter, from the owner's side of the seeded "Công ty Mẫu". Dialogs are opened for the
// picture only: nothing is created, switched, removed or deactivated.
const chapter = 'organizations'
const account = '[data-testid="app-header-user-menu"]'
const owned = 'main .rounded-xl:has-text("Công ty Mẫu")'
const members = '.n-modal [data-testid="organization-members-list"]'
const roles = '.n-modal [data-testid="organization-roles-list"]'

async function openOrganizations(page: Page) {
  await page.locator(owned).waitFor()
}

async function openMembers(page: Page) {
  await openOrganizations(page)
  await page.locator(`${owned} [data-testid="organization-members"]`).click()
  await page.locator(members).waitFor()
}

async function openRoles(page: Page) {
  await openOrganizations(page)
  await page.locator(`${owned} [data-testid="organization-roles"]`).click()
  await page.locator(roles).waitFor()
}

for (const locale of locales) {
  test(`switch [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale)
    await page.locator(account).click()
    await page.locator('.n-popover [data-testid="organization-list-manage"]').waitFor()
    await capture.annotate([
      { selector: account, n: 1, at: 'below' },
      { selector: '.n-popover button:has-text("Công ty Mẫu")', n: 2, at: 'after' },
      { selector: '[data-testid="organization-list-manage"]', n: 3, at: 'top-right' },
    ])
    await capture.blur([account, '.n-popover [role="menu"] > div:first-child'])
    await capture.shot({ locale, chapter, name: 'switch' })
  })

  test(`list [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await openOrganizations(page)
    await capture.annotate([
      { selector: 'main h1 + button', n: 1, at: 'below' },
      { selector: `${owned} .n-tag >> nth=0`, n: 2, at: 'after' },
      {
        selector: `${owned} button:has-text("${locale === 'vi' ? 'Chuyển' : 'Switch'}")`,
        n: 3,
        at: 'below',
      },
      { selector: `${owned} [data-testid="organization-members"]`, n: 4, at: 'below' },
      { selector: `${owned} [data-testid="organization-roles"]`, n: 5, at: 'below' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'list' })
  })

  test(`create [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await page.locator('main h1 + button').click()
    const form = '.n-dialog form'
    await page.locator(form).waitFor()
    await page
      .locator(`${form} input >> nth=0`)
      .fill(locale === 'vi' ? 'Phòng Kinh doanh' : 'Sales')
    await page
      .locator(`${form} input >> nth=1`)
      .fill(locale === 'vi' ? 'phong-kinh-doanh' : 'sales')
    await capture.annotate([
      { selector: `${form} .n-form-item >> nth=0`, n: 1, at: 'top-right' },
      { selector: `${form} .n-form-item >> nth=1`, n: 2, at: 'top-right' },
      { selector: '.n-dialog__action button >> nth=1', n: 3, at: 'below' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'create' })
  })

  test(`add-member [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await openMembers(page)
    const form = '.n-modal form'
    await capture.annotate([
      { selector: `${form} .n-form-item`, n: 1, at: 'top-right' },
      { selector: `${form} .n-base-selection`, n: 2, at: 'top-right' },
      { selector: `${form} button[type="submit"]`, n: 3, at: 'below' },
    ])
    await capture.blur([account, `${members} li p`])
    await capture.shot({ locale, chapter, name: 'add-member' })
  })

  test(`manage-members [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await openMembers(page)
    await capture.annotate([
      { selector: `${members} li >> nth=1 >> .n-base-selection`, n: 1, at: 'top-right' },
      { selector: `${members} li >> nth=1 >> button`, n: 2, at: 'below' },
    ])
    await capture.blur([account, `${members} li p`])
    await capture.shot({ locale, chapter, name: 'manage-members' })
  })

  test(`deactivate [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await openMembers(page)
    await capture.annotate([
      { selector: '[data-testid="organization-deactivate"]', n: 1, at: 'after' },
    ])
    await capture.blur([account, `${members} li p`])
    await capture.shot({ locale, chapter, name: 'deactivate' })
  })

  test(`roles [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await openRoles(page)
    const custom = `${roles} li:has-text("Trưởng nhóm")`
    await capture.annotate([
      { selector: `${roles} li >> nth=0 >> .n-tag >> nth=0`, n: 1, at: 'after' },
      { selector: `${custom} button >> nth=0`, n: 2, at: 'below' },
      { selector: `${custom} button >> nth=1`, n: 3, at: 'below' },
      { selector: '[data-testid="organization-roles-create"]', n: 4, at: 'after' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'roles' })
  })

  test(`role-form [${locale}]`, async ({ page, capture }) => {
    await capture.open(locale, '/#/organizations', owned)
    await openRoles(page)
    await page.locator('[data-testid="organization-roles-create"]').click()
    const form = '.n-dialog form'
    await page.locator(form).waitFor()
    await page.locator(`${form} input >> nth=0`).fill(locale === 'vi' ? 'Kế toán' : 'Accountant')
    await page.locator(`${form} .n-checkbox >> nth=1`).click()
    await capture.annotate([
      { selector: `${form} .n-form-item`, n: 1, at: 'top-right' },
      { selector: `${form} > div > label`, n: 2, at: 'after' },
      { selector: '.n-dialog__action button >> nth=1', n: 3, at: 'below' },
    ])
    await capture.blur([account])
    await capture.shot({ locale, chapter, name: 'role-form' })
  })
}
