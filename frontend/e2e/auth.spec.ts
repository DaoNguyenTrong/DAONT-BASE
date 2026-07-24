import { test, expect, login, logout, requireEnv } from './fixtures'

const ADMIN_USERNAME = requireEnv('E2E_ADMIN_USERNAME')
const ADMIN_PASSWORD = requireEnv('E2E_ADMIN_PASSWORD')
const USER_USERNAME = requireEnv('E2E_USER_USERNAME')
const USER_PASSWORD = requireEnv('E2E_USER_PASSWORD')

test.describe('login redirects', () => {
  test('user login redirects to home', async ({ page }) => {
    await login(page, USER_USERNAME, USER_PASSWORD)
    await expect(page).toHaveURL(/#\/$/)
    await logout(page)
  })

  test('admin login redirects to home', async ({ page }) => {
    await login(page, ADMIN_USERNAME, ADMIN_PASSWORD)
    await expect(page).toHaveURL(/#\/$/)
    await logout(page)
  })

  test('invalid credentials shows error and stays on login', async ({ page }) => {
    await login(page, 'e2e-nonexistent-user', 'wrong-password')
    await expect(page.getByText('Your username or password is incorrect.')).toBeVisible()
    await expect(page).toHaveURL(/#\/login$/)
  })
})
