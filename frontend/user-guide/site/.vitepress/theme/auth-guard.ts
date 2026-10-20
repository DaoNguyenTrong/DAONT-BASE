// nginx checks the session only on full page loads, but VitePress changes pages on the client, so a
// reader whose session expired would keep reading a private guide. On a 401 the next page change
// reloads the target through nginx, which sends them through sign-in and back.

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

/**
 * The guard's `navigate`. The target is already in the address bar, and re-assigning a URL with a
 * #fragment only scrolls; the fragment-less URL always loads from nginx, which drops it anyway.
 */
export function reloadThroughServer(to: string, location: Pick<Location, 'replace'>): void {
  location.replace(to.split('#')[0] ?? to)
}
