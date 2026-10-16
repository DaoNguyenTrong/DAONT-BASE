import { getAuth } from '@/api/generated/auth/auth'
import { reloadTo } from '@/lib/session-exit'

/**
 * Switching organization swaps the tenant every store's data was loaded for (members, roles,
 * notifications, permissions…), so it ends with a full reload to the home route — same reasoning as
 * a session exit (see lib/session-exit.ts). Home, not the current page: that page may belong to the
 * organization just left.
 *
 * Calls the endpoint directly instead of through the auth store: committing the response to the
 * store first would flip organizationId reactively, so the still-mounted page would refetch for the
 * new tenant (or render old-tenant lists with new-tenant permissions) right before the reload
 * discards it. The server already re-issued the auth cookies; the reloaded app's silent refresh
 * picks up the new organization. Errors propagate to the caller; nothing reloads on failure.
 */
export function useOrganizationSwitch() {
  const client = getAuth()
  const router = useRouter()

  async function switchOrganization(organizationId: string | null) {
    await client.authSwitchOrganization({ organizationId })
    reloadTo(router.resolve(useHomeRoute()).fullPath)
  }

  return { switchOrganization }
}
