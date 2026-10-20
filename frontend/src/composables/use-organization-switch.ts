import { getAuth } from '@/api/generated/auth/auth'
import { reloadTo } from '@/lib/session-exit'

/**
 * Full reload to home (the current page may belong to the org just left), so no store keeps the
 * old tenant's data. Skips the auth store, whose reactive organizationId would refetch the mounted
 * page first; the reload's silent refresh reads the re-issued cookies. Errors propagate, no reload.
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
