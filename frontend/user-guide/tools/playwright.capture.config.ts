import { fileURLToPath } from 'node:url'
import { defineConfig, devices } from '@playwright/test'
import { config as loadEnv } from 'dotenv'

// Screenshots for the user guide, taken from a running app: `bun run --cwd frontend guide:capture`.
// See frontend/user-guide/README.md § Chụp ảnh. No webServer — the app and the API must already run.
loadEnv({ path: fileURLToPath(new URL('./.env', import.meta.url)), quiet: true })

export default defineConfig({
  testDir: './sections',
  testMatch: '**/*.capture.ts',
  // One worker, one browser context: the backend rotates the refresh token on every refresh, and a
  // second context replaying the stored one would revoke the whole session (fixtures.ts § session).
  workers: 1,
  fullyParallel: false,
  retries: 0,
  reporter: 'list',
  outputDir: './.results',
  use: {
    ...devices['Desktop Chrome'],
    baseURL: process.env.GUIDE_BASE_URL ?? 'http://localhost:5175',
    viewport: { width: 1440, height: 900 },
    deviceScaleFactor: 1,
  },
  projects: [{ name: 'capture' }],
})
