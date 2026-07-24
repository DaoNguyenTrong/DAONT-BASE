import type { RouteMeta, RouteRecordRaw, Router } from 'vue-router'
import type { SidebarMenuItem } from '@/layouts/sidebar-menu'

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

function toSidebarItem(route: RouteRecordRaw): SidebarMenuItem | null {
  const sidebarMeta = route.meta?.sidebar as SidebarRouteMeta | undefined

  if (!sidebarMeta) {
    return null
  }

  const children = buildSidebarMenu(route.children ?? [])

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

function buildSidebarMenu(routes: readonly RouteRecordRaw[]) {
  return routes
    .filter((route) => Boolean(route.meta?.sidebar))
    .slice()
    .sort(bySidebarOrder)
    .map((route) => toSidebarItem(route))
    .filter((item): item is SidebarMenuItem => item !== null)
}

export function useSidebarMenu() {
  const router = useRouter()
  const version = getRouterVersion(router)

  return computed(() => {
    version.value

    const layoutRoute = router.options.routes.find((route) => route.path === '/')

    return buildSidebarMenu(layoutRoute?.children ?? [])
  })
}
