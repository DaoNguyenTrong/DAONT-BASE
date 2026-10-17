import { execSync } from 'node:child_process'

// Same source as the app footer (resolveAppVersion() in frontend/vite.config.ts), so the guide
// and the app it documents always show the same version. Node-only: never import from the theme.
export function resolveVersion(): string {
  try {
    return execSync('git describe --tags --always', { stdio: ['ignore', 'pipe', 'ignore'] })
      .toString()
      .trim()
  } catch {
    return 'unknown'
  }
}
