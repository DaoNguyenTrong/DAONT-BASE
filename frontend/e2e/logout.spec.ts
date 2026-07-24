import { test, expect, login, requireEnv } from './fixtures'

const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

test('logout returns to login and protected routes redirect afterwards', async ({ page }) => {
  await login(page, USER_USERNAME, USER_PASSWORD)
  await expect(page).toHaveURL(/#\/$/)

  await page.locator('aside').getByRole('button', { name: 'Logout' }).click()
  // Naive UI's dialog.warning() renders role="dialog", not "alertdialog"
  // (see fixtures.ts's logout() for the same fix).
  await page.getByRole('dialog').last().getByRole('button', { name: 'Confirm' }).click()
  await expect(page).toHaveURL(/#\/login$/)

  await page.goto('#/')
  await expect(page).toHaveURL(/#\/login$/)
})
