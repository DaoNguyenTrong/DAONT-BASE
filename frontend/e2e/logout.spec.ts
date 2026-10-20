import { test, expect, login, logout, requireEnv } from './fixtures'

const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

test('logout returns to login and protected routes redirect afterwards', async ({ page }) => {
  await login(page, USER_USERNAME, USER_PASSWORD)
  await expect(page).toHaveURL(/#\/$/)

  await logout(page)

  await page.evaluate(() => {
    window.location.hash = '#/'
  })
  await expect(page).toHaveURL(/#\/login(\?|$)/)
})
