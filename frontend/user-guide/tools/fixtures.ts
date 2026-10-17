import { chmodSync, existsSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { test as base, expect, type BrowserContext, type Page, type Route } from '@playwright/test'
import { login } from '../../e2e/fixtures'
import {
  imagePath,
  isAllowedApiRequest,
  markFits,
  markPosition,
  missingPreconditions,
  parseLocales,
  readSessionFailure,
  recordSessionFailure,
  type Box,
  type Locale,
  type MarkPlacement,
  type PreconditionData,
  type Tree,
} from './lib'

export { expect }

/** The languages this run captures (GUIDE_LOCALE). Sections loop over it, one test per language. */
export const locales = parseLocales(process.env.GUIDE_LOCALE)

const siteSrc = fileURLToPath(new URL('../site/src', import.meta.url))
const statePath = fileURLToPath(new URL('./.auth/state.json', import.meta.url))
// Playwright's outputDir (cleared at the start of every run); the runner process is shared by
// every worker of one run.
const resultsDir = fileURLToPath(new URL('./.results', import.meta.url))
const runId = String(process.ppid)
const apiUrl = (process.env.GUIDE_API_URL ?? 'http://localhost:7000').replace(/\/$/, '')
const APP_SHELL = 'aside'
const MARK_CLASS = 'guide-capture-mark'

// Not part of what the guide shows: the dev server's Vue DevTools button, and the footer's version
// and build time (they would change every image on every recapture).
const CAPTURE_CSS = [
  '#__vue-devtools-container__, .vue-devtools__anchor { display: none !important; }',
  'footer .sm\\:inline { visibility: hidden !important; }',
].join('\n')

function requireEnv(name: string): string {
  const value = process.env[name]
  if (!value)
    throw new Error(`Missing ${name}. Copy user-guide/tools/.env.example to .env and fill it in.`)
  return value
}

export type Capture = {
  /**
   * Opens a page of the app in the given language — set before the app loads, so each test loads
   * the app once (every load refreshes the session, and refreshes are rate-limited).
   */
  open(locale: Locale, path?: string): Promise<void>
  /**
   * Numbered violet marks, drawn by `shot`: by default inside each element's top-left corner;
   * `top-right` keeps a control at the left edge visible, `after` sits just right of a short element
   * such as a label. A missing element, or a mark outside the shot, fails the test.
   */
  annotate(items: { selector: string; n: number; at?: MarkPlacement }[]): Promise<void>
  /** CSS selectors of private data (names, email, IP, devices) to blur in the next shot. */
  blur(selectors: string[]): Promise<void>
  /** Writes site/src/<tree>/images/<locale>/<chapter>/<name>.jpg, then removes marks and blur. */
  shot(o: {
    tree?: Tree
    locale: Locale
    chapter: string
    name: string
    clip?: string
  }): Promise<void>
}

async function readPreconditions(context: BrowserContext): Promise<PreconditionData> {
  // X-TimeZone: every /api/* request needs it (UserTimeZoneMiddleware).
  const get = async <T>(path: string): Promise<T> => {
    const response = await context.request.get(`${apiUrl}${path}`, {
      headers: { 'X-TimeZone': 'UTC' },
    })
    if (!response.ok()) throw new Error(`GET ${path} → ${response.status()}`)
    return (await response.json()) as T
  }

  const organizations =
    await get<{ id: string; myPermissionCodes: string[] }[]>('/api/organizations')
  const membersByOrg: Record<string, number> = {}
  const customRolesByOrg: Record<string, number> = {}
  for (const { id } of organizations) {
    membersByOrg[id] = (await get<unknown[]>(`/api/organizations/${id}/members`)).length
    const roles = await get<{ isSystem: boolean }[]>(`/api/organizations/${id}/roles`)
    customRolesByOrg[id] = roles.filter((r) => !r.isSystem).length
  }
  const { count } = await get<{ count: number }>('/api/notifications/unread-count')
  const sessions = (await get<unknown[]>('/api/auth/sessions')).length

  return { organizations, membersByOrg, customRolesByOrg, unreadNotifications: count, sessions }
}

/** Signs in only if the stored session no longer works, then checks the account's data. */
async function openSession(context: BrowserContext): Promise<void> {
  const page = await context.newPage()
  try {
    await page.goto('/')
    await page.locator(`${APP_SHELL}, #username`).first().waitFor()
    if (!(await page.locator(APP_SHELL).isVisible())) {
      await login(page, requireEnv('GUIDE_USERNAME'), requireEnv('GUIDE_PASSWORD'))
      const outcome = page.locator(`${APP_SHELL}, form .n-alert`).first()
      await outcome.waitFor({ timeout: 15_000 })
      if (!(await page.locator(APP_SHELL).isVisible())) {
        throw new Error(`Sign-in failed: ${(await outcome.innerText()).trim()}`)
      }
    }
  } finally {
    await page.close()
  }

  const missing = missingPreconditions(await readPreconditions(context))
  if (missing.length > 0) {
    throw new Error(
      `The account's data is not ready for the screenshots:\n- ${missing.join('\n- ')}`,
    )
  }
}

async function saveState(context: BrowserContext): Promise<void> {
  mkdirSync(dirname(statePath), { recursive: true })
  await context.storageState({ path: statePath })
  // It holds a live refresh token.
  chmodSync(statePath, 0o600)
}

export const test = base.extend<
  { blockedWrites: string[]; capture: Capture },
  { session: BrowserContext }
>({
  // One context for the whole run, restored from and saved back to .auth/state.json. Every refresh
  // rotates the refresh token and a replayed old one revokes the session family, so contexts must not
  // share the file; and every sign-in adds a session the "Sessions" screenshot shows, so the tool
  // signs in only when the stored state no longer works.
  session: [
    async ({ browser }, use, workerInfo) => {
      // Playwright retries a failed worker fixture once per remaining test; a wrong password would
      // then sign in again and again until the account locks.
      const earlier = readSessionFailure(resultsDir, runId)
      if (earlier) throw new Error(`${earlier}\n(Stopped: an earlier test of this run hit it.)`)

      const context = await browser.newContext({
        ...workerInfo.project.use,
        // A service worker could send requests the write guard below never sees.
        serviceWorkers: 'block',
        storageState: existsSync(statePath) ? statePath : undefined,
      })
      try {
        await openSession(context)
      } catch (error) {
        recordSessionFailure(
          resultsDir,
          runId,
          error instanceof Error ? error.message : String(error),
        )
        // Keep the original error even if the browser is gone.
        await saveState(context).catch(() => {})
        await context.close()
        throw error
      }
      try {
        await use(context)
      } finally {
        await saveState(context)
        await context.close()
      }
    },
    { scope: 'worker' },
  ],

  blockedWrites: async ({}, use) => {
    await use([])
  },

  // A page of the shared context that can't change the system it photographs: any API write but
  // sign-in and refresh is aborted (on the context, so popups are covered too), and the test fails
  // listing what was attempted.
  page: async ({ session, blockedWrites }, use) => {
    const guard = (route: Route) => {
      const request = route.request()
      if (isAllowedApiRequest(request.method(), request.url())) return route.continue()
      blockedWrites.push(`${request.method()} ${request.url()}`)
      return route.abort('blockedbyclient')
    }
    await session.route('**/api/**', guard)
    const page = await session.newPage()
    await page.addInitScript((css) => {
      document.addEventListener('DOMContentLoaded', () => {
        const style = document.createElement('style')
        style.textContent = css
        document.head.appendChild(style)
      })
    }, CAPTURE_CSS)
    try {
      await use(page)
      expect(blockedWrites, 'A capture section must not write to the API').toEqual([])
    } finally {
      await page.close()
      await session.unroute('**/api/**', guard)
    }
  },

  capture: async ({ page, blockedWrites }, use) => {
    await use(createCapture(page, blockedWrites))
  },
})

function createCapture(page: Page, blockedWrites: string[]): Capture {
  let marks: { selector: string; n: number; at: MarkPlacement }[] = []
  let blurred: string[] = []

  return {
    async open(locale, path = '/') {
      await page.addInitScript((l) => window.localStorage.setItem('app-locale', l), locale)
      await page.goto(path)
      await page.locator(APP_SHELL).waitFor()
    },

    async annotate(items) {
      for (const { selector, n, at = 'top-left' } of items) {
        await expect(page.locator(selector).first(), `annotation ${n}: ${selector}`).toBeVisible()
        marks.push({ selector, n, at })
      }
    },

    async blur(selectors) {
      for (const selector of selectors) {
        await expect(page.locator(selector).first(), `blur: ${selector}`).toBeVisible()
        blurred.push(selector)
      }
    },

    async shot({ tree = 'base', locale, chapter, name, clip }) {
      // Don't overwrite a committed image from a test that is going to fail anyway.
      expect(blockedWrites, 'A capture section must not write to the API').toEqual([])
      const path = imagePath({ siteSrc, tree, locale, chapter, name })

      // Scroll first, measure after: the marks are pinned to the viewport.
      let area: Box
      if (clip) {
        const target = page.locator(clip).first()
        await target.scrollIntoViewIfNeeded()
        const box = await target.boundingBox()
        if (!box) throw new Error(`clip: ${clip} has no box`)
        area = box
      } else {
        const size = page.viewportSize()
        if (!size) throw new Error('The page has no viewport size')
        area = { x: 0, y: 0, ...size }
      }

      // A rule rather than inline styles: it also covers a node Vue re-renders before the shot.
      const blurStyle = blurred.length
        ? await page.addStyleTag({
            content: `${blurred.join(', ')} { filter: blur(6px) !important; }`,
          })
        : null

      for (const { selector, n, at } of marks) {
        const box = await page.locator(selector).first().boundingBox()
        if (!box) throw new Error(`annotation ${n}: ${selector} has no box`)
        const mark = markPosition(box, at)
        if (!markFits(mark, area)) {
          throw new Error(`annotation ${n}: ${selector} falls outside the shot`)
        }
        await page.evaluate(
          ({ x, y, size, n, cls }) => {
            const el = document.createElement('div')
            el.className = cls
            el.textContent = String(n)
            Object.assign(el.style, {
              position: 'fixed',
              left: `${x}px`,
              top: `${y}px`,
              width: `${size}px`,
              height: `${size}px`,
              borderRadius: '50%',
              background: '#7c3aed',
              color: '#fff',
              font: `600 15px/${size}px Roboto, system-ui, sans-serif`,
              textAlign: 'center',
              boxShadow: '0 0 0 3px #fff',
              zIndex: '2147483647',
              pointerEvents: 'none',
            })
            document.body.appendChild(el)
          },
          { ...mark, n, cls: MARK_CLASS },
        )
      }

      mkdirSync(dirname(path), { recursive: true })
      await page.screenshot({
        path,
        type: 'jpeg',
        quality: 82,
        animations: 'disabled',
        caret: 'hide',
        ...(clip ? { clip: area } : {}),
      })

      await page.evaluate(
        (cls) => document.querySelectorAll(`.${cls}`).forEach((el) => el.remove()),
        MARK_CLASS,
      )
      await blurStyle?.evaluate((el) => (el as Element).remove())
      marks = []
      blurred = []
    },
  }
}
