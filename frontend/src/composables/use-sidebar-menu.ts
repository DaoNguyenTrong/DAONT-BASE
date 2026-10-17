import type { RouteMeta, RouteRecordRaw, Router } from 'vue-router'
import { MENU_GROUPS } from '@/layouts/menu-groups'
import type { MenuGroup } from '@/layouts/menu-groups'
import type { SidebarMenuEntry, SidebarMenuItem } from '@/layouts/sidebar-menu'

type SidebarRouteMeta = NonNullable<RouteMeta['sidebar']>

const routerVersions = new WeakMap<Router, Ref<number>>()

function getRouterVersion(router: Router) {
  const existing = routerVersions.get(router)

  if (existing) {
    return existing
  }

  const version = ref(0)
  const addRoute = router.addRoute as (...args: any[]) => () => void

  router.addRoute = ((...args: any[]) => {
    const removeRoute = addRoute(...args)

    version.value += 1

    return () => {
      removeRoute()
      version.value += 1
    }
  }) as Router['addRoute']

  routerVersions.set(router, version)

  return version
}

function bySidebarOrder(a: RouteRecordRaw, b: RouteRecordRaw) {
  return (a.meta?.sidebar?.order ?? 0) - (b.meta?.sidebar?.order ?? 0)
}

function toSidebarItem(
  route: RouteRecordRaw,
  hasPermission: (code: string) => boolean,
): SidebarMenuItem | null {
  const sidebarMeta = route.meta?.sidebar as SidebarRouteMeta | undefined

  if (!sidebarMeta) {
    return null
  }

  const children = buildSidebarItems(route.children ?? [], hasPermission)

  if (children.length > 0) {
    return {
      labelKey: sidebarMeta.labelKey,
      icon: sidebarMeta.icon,
      items: children,
    }
  }

  if (route.redirect || typeof route.name !== 'string') {
    return null
  }

  return {
    labelKey: sidebarMeta.labelKey,
    icon: sidebarMeta.icon,
    routeName: route.name,
  }
}

function buildSidebarItems(
  routes: readonly RouteRecordRaw[],
  hasPermission: (code: string) => boolean,
): SidebarMenuItem[] {
  return (
    routes
      .filter((route) => Boolean(route.meta?.sidebar))
      // Same gate as the router guard, so the menu never offers a page the guard would bounce.
      .filter((route) => !route.meta?.permission || hasPermission(route.meta.permission))
      .slice()
      .sort(bySidebarOrder)
      .map((route) => toSidebarItem(route, hasPermission))
      .filter((item): item is SidebarMenuItem => item !== null)
  )
}

export function buildMenuEntries(
  routes: readonly RouteRecordRaw[],
  hasPermission: (code: string) => boolean,
  groups: readonly MenuGroup[],
): SidebarMenuEntry[] {
  const knownGroups = new Map(groups.map((group) => [group.id, group]))
  const grouped = new Map<string, SidebarMenuItem[]>()
  const ranked: { order: number; entry: SidebarMenuEntry }[] = []

  const visible = routes
    .filter((route) => Boolean(route.meta?.sidebar))
    .filter((route) => !route.meta?.permission || hasPermission(route.meta.permission))
    .slice()
    .sort(bySidebarOrder)

  for (const route of visible) {
    const item = toSidebarItem(route, hasPermission)

    if (!item) {
      continue
    }

    const groupId = (route.meta?.sidebar as SidebarRouteMeta).group

    if (groupId && knownGroups.has(groupId)) {
      grouped.set(groupId, [...(grouped.get(groupId) ?? []), item])
      continue
    }

    if (groupId && import.meta.env.DEV) {
      console.warn(`[sidebar] Unknown menu group "${groupId}"; showing the item ungrouped.`)
    }

    ranked.push({
      order: route.meta?.sidebar?.order ?? 0,
      entry: { ...item, kind: 'item' },
    })
  }

  for (const group of groups) {
    const items = grouped.get(group.id)

    if (items?.length) {
      ranked.push({
        order: group.order,
        entry: {
          kind: 'group',
          id: group.id,
          labelKey: group.labelKey,
          icon: group.icon,
          items,
        },
      })
    }
  }

  return ranked.sort((a, b) => a.order - b.order).map(({ entry }) => entry)
}

export function useSidebarMenu(groups: readonly MenuGroup[] = MENU_GROUPS) {
  const router = useRouter()
  const auth = useAuthStore()
  const version = getRouterVersion(router)

  return computed(() => {
    version.value

    const layoutRoute = router.options.routes.find((route) => route.path === '/')

    return buildMenuEntries(layoutRoute?.children ?? [], auth.hasPermission, groups)
  })
}
