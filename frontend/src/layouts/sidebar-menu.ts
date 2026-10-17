import type { Component } from 'vue'

export interface SidebarMenuItem {
  labelKey: string
  icon: Component
  routeName?: string
  items?: SidebarMenuItem[]
}

export interface SidebarMenuGroup {
  kind: 'group'
  id: string
  labelKey: string
  icon: Component
  items: SidebarMenuItem[]
}

export type SidebarMenuLink = SidebarMenuItem & { kind: 'item' }

export type SidebarMenuEntry = SidebarMenuGroup | SidebarMenuLink

export interface FlatMenuLink {
  labelKey: string
  icon: Component
  routeName: string
  trailKeys: string[]
}

export function flattenMenuLinks(entries: SidebarMenuEntry[]): FlatMenuLink[] {
  const links: FlatMenuLink[] = []

  function visit(item: SidebarMenuItem, trailKeys: string[]) {
    if (item.items?.length) {
      item.items.forEach((child) => visit(child, [...trailKeys, item.labelKey]))
    } else if (item.routeName) {
      links.push({
        labelKey: item.labelKey,
        icon: item.icon,
        routeName: item.routeName,
        trailKeys,
      })
    }
  }

  for (const entry of entries) {
    if (entry.kind === 'group') {
      entry.items.forEach((item) => visit(item, [entry.labelKey]))
    } else {
      visit(entry, [])
    }
  }

  return links
}

export function containsRoute(
  entry: SidebarMenuEntry | SidebarMenuItem,
  routeName: string | undefined,
): boolean {
  if (!routeName) {
    return false
  }

  if ('kind' in entry && entry.kind === 'group') {
    return entry.items.some((item) => containsRoute(item, routeName))
  }

  const item = entry as SidebarMenuItem

  return (
    item.routeName === routeName || Boolean(item.items?.some((c) => containsRoute(c, routeName)))
  )
}
