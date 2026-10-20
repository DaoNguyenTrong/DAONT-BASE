import { test as base, expect, type Page } from '@playwright/test'

export const test = base.extend({
  page: async ({ page }, use) => {
    await page.addInitScript(() => window.localStorage.setItem('app-locale', 'en'))
    await use(page)
  },
})

export { expect }

export async function login(page: Page, username: string, password: string) {
  await page.goto('#/login')
  await page.locator('#username').fill(username)
  await page.locator('#password').fill(password)
  await page.locator('form button[type="submit"]').click()
}

export async function logout(page: Page) {
  await page.locator('[data-testid="app-header-user-menu"]').click()
  await page.locator('[data-testid="user-menu-logout"]').click()
  // Logout ends with a full page reload (reloadTo in lib/session-exit.ts), and the URL changes before the reload
  // starts. Wait for the reloaded document itself, or the next step runs against the old one and gets interrupted.
  const reloaded = page.waitForEvent('load')
  // Naive UI's dialog.warning() renders role="dialog" (hardcoded in the
  // library, no alertdialog option). .last() defends against another dialog
  // already being open when this shared helper runs.
  await page.getByRole('dialog').last().getByRole('button', { name: 'Confirm' }).click()
  await reloaded
  await expect(page).toHaveURL(/#\/login$/)
}

export function requireEnv(name: string): string {
  const value = process.env[name]
  if (!value) {
    throw new Error(`Missing env var ${name}. Copy .env.e2e.example to .env.e2e and fill it in.`)
  }
  return value
}
