// The capture tool's rules, free of Playwright so the frontend's vitest can load them
// (tests/unit/user-guide-tools/lib.test.ts).

import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs'
import { join } from 'node:path'

export type Locale = 'vi' | 'en'
export type Tree = 'base' | 'app'

const LOCALES: Locale[] = ['vi', 'en']

/** GUIDE_LOCALE → the languages to capture. A typo stops the run instead of capturing nothing. */
export function parseLocales(value: string | undefined): Locale[] {
  if (value === undefined || value === '' || value === 'all') return [...LOCALES]
  if ((LOCALES as string[]).includes(value)) return [value as Locale]
  throw new Error(`GUIDE_LOCALE=${value} is not one of vi|en|all`)
}

// Signing in and the app's silent refresh are the only writes a capture run needs.
const ALLOWED_WRITES = ['/api/auth/login', '/api/auth/refresh']

/**
 * Screenshots are taken from a real system: a section that clicks Save, Delete or "mark as read"
 * must not change it. Everything but reads, sign-in and refresh is refused.
 */
export function isAllowedApiRequest(method: string, url: string): boolean {
  const verb = method.toUpperCase()
  if (verb === 'GET' || verb === 'HEAD' || verb === 'OPTIONS') return true
  return verb === 'POST' && ALLOWED_WRITES.includes(new URL(url).pathname)
}

const SLUG = /^[a-z0-9]+(?:-[a-z0-9]+)*$/

/** Where a screenshot goes in the site; the chapter is 1–2 kebab-case segments, the name one. */
export function imagePath(o: {
  siteSrc: string
  tree: Tree
  locale: Locale
  chapter: string
  name: string
}): string {
  const segments = o.chapter.split('/')
  if (segments.length > 2 || !segments.every((s) => SLUG.test(s))) {
    throw new Error(`Chapter "${o.chapter}" must be one or two kebab-case segments`)
  }
  if (!SLUG.test(o.name)) throw new Error(`Image name "${o.name}" must be kebab-case`)
  return `${o.siteSrc}/${o.tree}/images/${o.locale}/${o.chapter}/${o.name}.jpg`
}

export type PreconditionData = {
  organizations: { id: string; myPermissionCodes: string[] }[]
  /** Organization id → member count. */
  membersByOrg: Record<string, number>
  /** Organization id → roles with isSystem = false. */
  customRolesByOrg: Record<string, number>
  unreadNotifications: number
  sessions: number
}

// Only an organization's owner holds this permission by default.
export const OWNER_PERMISSION = 'organizations.manage'

/**
 * The data the guide's screenshots show (spec §10). The tool never creates it — every unmet
 * condition is reported at once, so one fix-up round is enough.
 */
export function missingPreconditions(d: PreconditionData): string[] {
  const missing: string[] = []
  const owned = d.organizations.filter((o) => o.myPermissionCodes.includes(OWNER_PERMISSION))

  if (owned.length === 0) {
    missing.push('The account must own an organization (Owner role)')
    missing.push('No owned organization to check for 2+ members and a custom role')
  } else {
    const members = (id: string) => d.membersByOrg[id] ?? 0
    const roles = (id: string) => d.customRolesByOrg[id] ?? 0
    if (!owned.some((o) => members(o.id) >= 2 && roles(o.id) >= 1)) {
      const id = owned[0]!.id
      const parts: string[] = []
      if (members(id) < 2) parts.push(`2+ members (has ${members(id)})`)
      if (roles(id) < 1) parts.push(`a custom role (has ${roles(id)})`)
      missing.push(`The owned organization needs ${parts.join(' and ')}`)
    }
  }

  if (d.unreadNotifications < 1) missing.push('The account needs an unread notification')
  if (d.sessions < 2) missing.push(`The account needs 2+ signed-in sessions (has ${d.sessions})`)
  return missing
}

export type Box = { x: number; y: number; width: number; height: number }
export type MarkPlacement = 'top-left' | 'top-right' | 'before' | 'after' | 'below'
export type Mark = { x: number; y: number; size: number }

const MARK_SIZE = 28

/**
 * Where a numbered mark goes: inside the element's top-left corner, inside its top-right corner
 * (keeps a control at the left edge visible), just left or right of it (a short label, or a control
 * hemmed in on one side), or just under it (a small icon button).
 */
export function markPosition(box: Box, at: MarkPlacement): Mark {
  if (at === 'below') {
    return { x: box.x + (box.width - MARK_SIZE) / 2, y: box.y + box.height + 6, size: MARK_SIZE }
  }
  if (at === 'before') {
    return { x: box.x - MARK_SIZE - 8, y: box.y + (box.height - MARK_SIZE) / 2, size: MARK_SIZE }
  }
  if (at === 'after') {
    return { x: box.x + box.width + 8, y: box.y + (box.height - MARK_SIZE) / 2, size: MARK_SIZE }
  }
  const x = at === 'top-right' ? box.x + box.width - MARK_SIZE - 4 : box.x + 4
  return { x, y: box.y + 4, size: MARK_SIZE }
}

/** Whether a mark lies wholly inside the shot (the viewport, or the clipped area). */
export function markFits(mark: Mark, area: Box): boolean {
  return (
    mark.x >= area.x &&
    mark.y >= area.y &&
    mark.x + mark.size <= area.x + area.width &&
    mark.y + mark.size <= area.y + area.height
  )
}

// Playwright starts a fresh worker after a worker fixture fails, and that worker runs the fixture
// again: a wrong password would sign in (and fail) once per test until the account locks. The first
// failure is written down for the run, and later workers fail on it without signing in.
const failureFile = (dir: string, runId: string) => join(dir, `session-failed-${runId}.txt`)

export function recordSessionFailure(dir: string, runId: string, message: string): void {
  mkdirSync(dir, { recursive: true })
  writeFileSync(failureFile(dir, runId), message)
}

export function readSessionFailure(dir: string, runId: string): string | null {
  const file = failureFile(dir, runId)
  return existsSync(file) ? readFileSync(file, 'utf8') : null
}
