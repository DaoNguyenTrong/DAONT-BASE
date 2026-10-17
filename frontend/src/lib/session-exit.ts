import { stringifyQuery, type LocationQueryRaw } from 'vue-router'

let reloading = false

/**
 * Leaves the current session with a full page reload instead of an in-SPA navigation. Every Pinia
 * store is a page-lifetime singleton, so `router.replace` would hand the previous account's (or the
 * previous organization's) state — unread count, permissions, cached lists — to whoever comes next
 * in the same tab. A reload wipes every store, including ones added later, with no reset list to
 * keep in sync.
 *
 * Only the first call takes effect: logout also flips `isAuthenticated`, which trips the
 * session-lapse watcher in `App.vue`, and that second exit must not override the first one's
 * destination.
 */
export function reloadTo(path: string) {
  if (reloading) return

  reloading = true
  // replaceState rather than assigning location.hash: a hash change alone neither reloads the page
  // nor should be picked up by the router's own hashchange listener in the meantime.
  window.history.replaceState(null, '', `#${path}`)
  window.location.reload()
}

/** Call after the auth state is cleared, so the reloaded app doesn't attempt a silent refresh. */
export function reloadToLogin(query: LocationQueryRaw = {}) {
  const search = stringifyQuery(query)
  reloadTo(`/login${search ? `?${search}` : ''}`)
}
