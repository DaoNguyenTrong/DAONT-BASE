// nginx checks the session on every full page load of a private guide, but VitePress changes pages
// on the client after the first one. Without this guard a reader whose session ran out (the access
// cookie lives 15 minutes) keeps reading until the next reload; with it, the next page change reloads
// the target through nginx, which sends them through sign-in and back.

export type GuardDeps = {
  /** Text of /guide/_mode (`private` | `public`); read once. */
  mode: () => Promise<string>
  /** Status of GET /api/auth/check on this origin. */
  check: () => Promise<number>
  /** Full page load. */
  navigate: (to: string) => void
}

export function createAuthGuard(deps: GuardDeps): (to: string) => Promise<boolean | void> {
  let first = true
  let mode: Promise<string> | null = null

  return async (to) => {
    // VitePress loads the page nginx has just served through the same hook.
    if (first) {
      first = false
      return
    }
    try {
      mode ??= deps.mode().catch((error: unknown) => {
        mode = null
        throw error
      })
      if ((await mode) !== 'private') return
      if ((await deps.check()) === 401) {
        deps.navigate(to)
        return false
      }
    } catch {
      // Offline or API down: let it through; the next full page load still goes through nginx.
    }
  }
}
