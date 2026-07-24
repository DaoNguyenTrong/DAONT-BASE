import type { Page, BrowserContext } from '@playwright/test'
import { test, expect, login, logout, requireEnv } from './fixtures'

test.describe.configure({ mode: 'serial' })

const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

let context: BrowserContext
let page: Page

test.beforeAll(async ({ browser }) => {
  // Simulate a second device: log in once, then close it. The session persists
  // server-side — this gives the "current device" context something to see as an "other" active session.
  const otherContext = await browser.newContext()
  const otherPage = await otherContext.newPage()
  await otherPage.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await login(otherPage, USER_USERNAME, USER_PASSWORD)
  await expect(otherPage).toHaveURL(/#\/$/)
  await otherContext.close()

  // Now the "current device" under test.
  context = await browser.newContext()
  page = await context.newPage()
  await page.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
  await login(page, USER_USERNAME, USER_PASSWORD)
  await expect(page).toHaveURL(/#\/$/)
})

test.afterAll(async () => {
  await page.keyboard.press('Escape') // close the modal Profile dialog before logging out
  await logout(page)
  await context.close()
})

test('profile dialog shows active sessions including another device', async () => {
  await page.locator('aside').getByRole('button', { name: 'Profile' }).first().click()
  await expect(page.getByRole('dialog', { name: 'Profile' })).toBeVisible()
  await page.getByRole('tab', { name: 'Login sessions' }).click()
  await expect(page.getByText('This device')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Sign out other devices' })).toBeVisible()
})

test('revoke other devices leaves current session signed in', async () => {
  await page.getByRole('button', { name: 'Sign out other devices' }).click()
  // Naive UI's dialog.warning() renders role="dialog" (hardcoded in the
  // library, no alertdialog option). .last() targets the confirm popup,
  // which mounts after (and on top of) the still-open profile dialog.
  await page.getByRole('dialog').last().getByRole('button', { name: 'Confirm' }).click()
  await expect(page.getByText('Other devices signed out')).toBeVisible()
  await expect(page.getByText('This device')).toBeVisible()
  await expect(page.getByRole('button', { name: 'Sign out other devices' })).toHaveCount(0)
})
