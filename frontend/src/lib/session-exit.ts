import { stringifyQuery, type LocationQueryRaw } from 'vue-router'

let reloading = false

/**
 * Full reload, not SPA navigation: it wipes every Pinia store, so no account's or organization's
 * state (permissions, cached lists) leaks to the next user in the tab. Only the first call counts —
 * logout also trips App.vue's session-lapse watcher, which must not override the destination.
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
