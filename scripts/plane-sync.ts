// Moves the Plane work items a PR serves (docs/process/lifecycle.md § Plane). Fails open: always exits 0.
// Usage: git log --format=%B <base>..<head> | bun scripts/plane-sync.ts opened|merged|closed

import { existsSync, readFileSync } from 'node:fs'

export type SyncEvent = 'opened' | 'merged' | 'closed'

const STATE_KEYS = ['inProgress', 'readyForTesting', 'testing', 'done', 'pending', 'cancelled'] as const
type States = Record<(typeof STATE_KEYS)[number], string>
type Config = { api: string; projectId: string; projectIdentifier: string; states: States }

const EVENTS: Record<SyncEvent, { state: keyof States; link: boolean }> = {
  opened: { state: 'readyForTesting', link: true },
  merged: { state: 'testing', link: true },
  closed: { state: 'inProgress', link: false },
}

const MAX_IDS = 50
const TIMEOUT_MS = 10_000
// Everything put into a request URL must match this, so no value can add a path or query.
const SAFE_ID = /^[A-Za-z0-9_-]{1,64}$/

const isSafeId = (value: unknown): value is string => typeof value === 'string' && SAFE_ID.test(value)

export function parseIds(texts: string[], identifier: string): string[] {
  const token = new RegExp(`^${identifier}-[0-9]{1,9}$`)
  const ids = new Set<string>()
  for (const line of texts.flatMap((t) => t.split(/\r?\n/))) {
    if (!line.startsWith('Plane:')) continue
    const value = line.slice('Plane:'.length).trim()
    // The reason after `none` may name other items; it serves none of them.
    if (/^none\b/i.test(value)) continue
    for (const part of value.split(/[\s,]+/)) if (token.test(part)) ids.add(part)
  }
  return [...ids].slice(0, MAX_IDS)
}

export function planItem(event: SyncEvent, state: string, states: States): { link: boolean; moveTo?: string } {
  const { state: target, link } = EVENTS[event]
  const settled = [states.done, states.cancelled, states.pending].includes(state)
  return { link, moveTo: settled || state === states[target] ? undefined : states[target] }
}

function parseUrl(value: unknown): URL | undefined {
  try {
    const url = new URL(String(value))
    return url.username || url.password ? undefined : url
  } catch {
    return undefined
  }
}

// Returns the reason as a string when the config cannot be used.
function readConfig(text: string): Config | string {
  let raw: any
  try {
    raw = JSON.parse(text)
  } catch {
    return '.claude/plane.json is not valid JSON'
  }
  const base = parseUrl(raw?.baseUrl)
  if (base?.protocol !== 'https:' || base.search || base.hash) return 'baseUrl must be a plain https URL'
  const { workspaceSlug, projectId, projectIdentifier, states } = raw
  if (!isSafeId(workspaceSlug) || !isSafeId(projectId)) return 'workspaceSlug or projectId is missing or invalid'
  if (typeof projectIdentifier !== 'string' || !/^[A-Za-z0-9]{1,20}$/.test(projectIdentifier))
    return 'projectIdentifier is missing or invalid'
  // All of them: without the done, pending and cancelled ids a settled item could be moved.
  if (!STATE_KEYS.every((key) => isSafeId(states?.[key]))) return `states needs ${STATE_KEYS.join(', ')}`
  const api = `${base.href.replace(/\/+$/, '')}/api/v1/workspaces/${workspaceSlug}/`
  return { api, projectId, projectIdentifier, states }
}

export type SyncInput = {
  event: SyncEvent
  configText: string | undefined
  token: string | undefined
  description: string
  commits: string
  prUrl: string | undefined
  fetchFn: (url: string, init: RequestInit) => Promise<Response>
  log: (message: string) => void
  warn: (message: string) => void
}

class HttpError extends Error {}

// Only our own text and error names: a runtime's error message can quote the request headers.
const reason = (error: unknown) =>
  error instanceof HttpError ? error.message : error instanceof Error ? error.name : 'error'

export async function runSync(input: SyncInput): Promise<void> {
  const { event, configText, fetchFn, log, warn } = input
  if (!Object.hasOwn(EVENTS, event)) return warn('unknown event, expected opened, merged or closed')
  if (configText === undefined) return warn('no .claude/plane.json, nothing to sync')
  const config = readConfig(configText)
  if (typeof config === 'string') return warn(config)
  const token = input.token?.trim()
  if (!token) return warn('PLANE_API_KEY is not set')
  const ids = parseIds([input.description, input.commits], config.projectIdentifier)
  if (ids.length === 0) return warn('no work item id on a "Plane:" line')
  const link = parseUrl(input.prUrl)
  const prUrl = link && /^https?:$/.test(link.protocol) ? input.prUrl : undefined

  const { api, projectId, states } = config
  const call = async (method: string, path: string, body?: object): Promise<any> => {
    const response = await fetchFn(`${api}${path}`, {
      method,
      headers: { 'X-Api-Key': token, 'Content-Type': 'application/json' },
      body: body && JSON.stringify(body),
      // A followed redirect would carry the key to whatever host the answer names.
      redirect: 'manual',
      signal: AbortSignal.timeout(TIMEOUT_MS),
    })
    if (!response.ok) throw new HttpError(`HTTP ${response.status} on ${method} ${path}`)
    return method === 'GET' ? response.json() : undefined
  }

  const addLink = async (id: string, item: string, url: string) => {
    try {
      const { results } = await call('GET', `${item}links/`)
      if (!Array.isArray(results)) throw new HttpError('unexpected link list')
      if (!results.some((l) => l?.url === url)) await call('POST', `${item}links/`, { url, title: 'Pull request' })
    } catch (error) {
      warn(`${id}: link not added (${reason(error)})`)
    }
  }

  for (const id of ids) {
    try {
      const found = await call('GET', `work-items/${id}/`)
      if (!isSafeId(found?.id) || typeof found.state !== 'string') throw new HttpError('unexpected work item')
      if (found.project !== projectId) throw new HttpError('work item is not in the configured project')
      const item = `projects/${projectId}/work-items/${found.id}/`
      const plan = planItem(event, found.state, states)
      if (plan.link && prUrl) await addLink(id, item, prUrl)
      if (plan.moveTo) await call('PATCH', item, { state: plan.moveTo })
      log(`${id}: ${plan.moveTo ? `moved to ${EVENTS[event].state}` : 'state unchanged'}`)
    } catch (error) {
      warn(`${id}: not synced (${reason(error)})`)
    }
  }
}

if (import.meta.main) {
  const env = process.env
  const prefix = env.GITHUB_ACTIONS === 'true' ? '::warning::' : 'warning: '
  const warn = (message: string) => console.log(`${prefix}plane-sync: ${message}`)
  try {
    const path = '.claude/plane.json'
    await runSync({
      event: process.argv[2] as SyncEvent,
      configText: existsSync(path) ? readFileSync(path, 'utf8') : undefined,
      token: env.PLANE_API_KEY,
      // GitLab predefines the description (possibly truncated); GitHub passes it as PLANE_PR_BODY.
      description: env.PLANE_PR_BODY ?? env.CI_MERGE_REQUEST_DESCRIPTION ?? '',
      commits: process.stdin.isTTY ? '' : await Bun.stdin.text(),
      prUrl: env.PLANE_PR_URL,
      fetchFn: fetch,
      log: (message) => console.log(`plane-sync: ${message}`),
      warn,
    })
  } catch (error) {
    warn(reason(error))
  }
  process.exit(0)
}
