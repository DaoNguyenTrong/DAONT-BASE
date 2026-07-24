import type { Page, BrowserContext } from '@playwright/test'
import { test, expect, login, logout, requireEnv } from './fixtures'

test.describe.configure({ mode: 'serial' })

const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

let context: BrowserContext
let page: Page

test.beforeAll(async ({ browser }) => {
  context = await browser.newContext()
  page = await context.newPage()
  await page.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await login(page, USER_USERNAME, USER_PASSWORD)
  await expect(page).toHaveURL(/#\/$/)
})

test.afterAll(async () => {
  await logout(page)
  await context.close()
})

test('non-admin visiting /accounts is redirected to home', async () => {
  await page.goto('#/accounts')
  await expect(page).toHaveURL(/#\/$/)
})

test('authenticated user visiting /login is redirected to home', async () => {
  await page.goto('#/login')
  await expect(page).toHaveURL(/#\/$/)
})

test('unauthenticated visitor to /accounts is redirected to login', async ({ browser }) => {
  const anonContext = await browser.newContext()
  const anonPage = await anonContext.newPage()
  await anonPage.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await anonPage.goto('#/accounts')
  await expect(anonPage).toHaveURL(/#\/login$/)
  await anonContext.close()
})
