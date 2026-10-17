import { existsSync, mkdirSync } from 'node:fs'
import { dirname } from 'node:path'
import { fileURLToPath } from 'node:url'
import { test as base, expect, type BrowserContext, type Page } from '@playwright/test'
import { login } from '../../e2e/fixtures'
import {
  imagePath,
  isAllowedApiRequest,
  missingPreconditions,
  parseLocales,
  type Locale,
  type PreconditionData,
  type Tree,
} from './lib'

export { expect }

/** The languages this run captures (GUIDE_LOCALE). Sections loop over it, one test per language. */
export const locales = parseLocales(process.env.GUIDE_LOCALE)

const siteSrc = fileURLToPath(new URL('../site/src', import.meta.url))
const statePath = fileURLToPath(new URL('./.auth/state.json', import.meta.url))
const apiUrl = (process.env.GUIDE_API_URL ?? 'http://localhost:7000').replace(/\/$/, '')
const APP_SHELL = 'aside'
const MARK_CLASS = 'guide-capture-mark'
const BLUR_ATTR = 'data-guide-capture-blur'

function requireEnv(name: string): string {
  const value = process.env[name]
  if (!value)
    throw new Error(`Missing ${name}. Copy user-guide/tools/.env.example to .env and fill it in.`)
  return value
}

export type Capture = {
  /** Switches the app's language the way the app stores it, then reloads. */
  useLocale(locale: Locale): Promise<void>
  /**
   * Numbered violet marks, by default inside each element's top-left corner; `top-right` keeps a
   * control at the left edge visible, `after` sits just right of a short element such as a label.
   * A missing element fails the test.
   */
  annotate(
    items: { selector: string; n: number; at?: 'top-left' | 'top-right' | 'after' }[],
  ): Promise<void>
  /** Blurs private data (email, IP, device names); a missing element fails the test. */
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

export const test = base.extend<{ capture: Capture }, { session: BrowserContext }>({
  // One context for the whole run, restored from and saved back to .auth/state.json. Every refresh
  // rotates the refresh token and a replayed old one revokes the session family, so contexts must not
  // share the file; and every sign-in adds a session the "Sessions" screenshot shows, so the tool
  // signs in only when the stored state no longer works.
  session: [
    async ({ browser }, use, workerInfo) => {
      const context = await browser.newContext({
        ...workerInfo.project.use,
        storageState: existsSync(statePath) ? statePath : undefined,
      })
      try {
        const page = await context.newPage()
        await page.goto('/')
        await page.locator(`${APP_SHELL}, #username`).first().waitFor()
        if (!(await page.locator(APP_SHELL).isVisible())) {
          await login(page, requireEnv('GUIDE_USERNAME'), requireEnv('GUIDE_PASSWORD'))
          await page.locator(APP_SHELL).waitFor()
        }
        await page.close()

        const missing = missingPreconditions(await readPreconditions(context))
        if (missing.length > 0) {
          throw new Error(
            `The account's data is not ready for the screenshots:\n- ${missing.join('\n- ')}`,
          )
        }

        await use(context)
      } finally {
        mkdirSync(dirname(statePath), { recursive: true })
        await context.storageState({ path: statePath })
        await context.close()
      }
    },
    { scope: 'worker' },
  ],

  // A page of the shared context that can't change the system it photographs: any API write but
  // sign-in and refresh is aborted, and the test fails listing what was attempted.
  page: async ({ session }, use) => {
    const page = await session.newPage()
    // The dev server's Vue DevTools button is not part of the app.
    await page.addInitScript(() =>
      document.addEventListener('DOMContentLoaded', () => {
        const style = document.createElement('style')
        style.textContent =
          '#__vue-devtools-container__, .vue-devtools__anchor { display: none !important; }'
        document.head.appendChild(style)
      }),
    )
    const blocked: string[] = []
    await page.route('**/api/**', (route) => {
      const request = route.request()
      if (isAllowedApiRequest(request.method(), request.url())) return route.continue()
      blocked.push(`${request.method()} ${request.url()}`)
      return route.abort('blockedbyclient')
    })
    try {
      await use(page)
      expect(blocked, 'A capture section must not write to the API').toEqual([])
    } finally {
      await page.close()
    }
  },

  capture: async ({ page }, use) => {
    await use(createCapture(page))
  },
})

const MARK_SIZE = 28

function markPosition(
  box: { x: number; y: number; width: number; height: number },
  at: 'top-left' | 'top-right' | 'after',
): { x: number; y: number } {
  if (at === 'after') return { x: box.x + box.width + 8, y: box.y + (box.height - MARK_SIZE) / 2 }
  return { x: at === 'top-right' ? box.x + box.width - MARK_SIZE - 4 : box.x + 4, y: box.y + 4 }
}

function createCapture(page: Page): Capture {
  return {
    async useLocale(locale) {
      if (!page.url().startsWith('http')) await page.goto('/')
      await page.evaluate((l) => window.localStorage.setItem('app-locale', l), locale)
      await page.reload()
      await page.locator(APP_SHELL).waitFor()
    },

    async annotate(items) {
      for (const { selector, n, at = 'top-left' } of items) {
        const target = page.locator(selector).first()
        await expect(target, `annotation ${n}: ${selector}`).toBeVisible()
        const box = await target.boundingBox()
        if (!box) throw new Error(`annotation ${n}: ${selector} has no box`)
        await page.evaluate(
          ({ x, y, n, cls }) => {
            const mark = document.createElement('div')
            mark.className = cls
            mark.textContent = String(n)
            Object.assign(mark.style, {
              position: 'fixed',
              left: `${x}px`,
              top: `${y}px`,
              width: '28px',
              height: '28px',
              borderRadius: '50%',
              background: '#7c3aed',
              color: '#fff',
              font: '600 15px/28px Roboto, system-ui, sans-serif',
              textAlign: 'center',
              boxShadow: '0 0 0 3px #fff',
              zIndex: '2147483647',
              pointerEvents: 'none',
            })
            document.body.appendChild(mark)
          },
          {
            ...markPosition(box, at),
            n,
            cls: MARK_CLASS,
          },
        )
      }
    },

    async blur(selectors) {
      for (const selector of selectors) {
        const targets = page.locator(selector)
        await expect(targets.first(), `blur: ${selector}`).toBeVisible()
        await targets.evaluateAll(
          (elements, attr) =>
            elements.forEach((el) => {
              ;(el as HTMLElement).style.filter = 'blur(6px)'
              el.setAttribute(attr, '')
            }),
          BLUR_ATTR,
        )
      }
    },

    async shot({ tree = 'base', locale, chapter, name, clip }) {
      const path = imagePath({ siteSrc, tree, locale, chapter, name })
      mkdirSync(dirname(path), { recursive: true })
      const target = clip ? page.locator(clip).first() : page
      await target.screenshot({
        path,
        type: 'jpeg',
        quality: 82,
        animations: 'disabled',
        caret: 'hide',
      })
      await page.evaluate(
        ({ cls, attr }) => {
          document.querySelectorAll(`.${cls}`).forEach((el) => el.remove())
          document.querySelectorAll(`[${attr}]`).forEach((el) => {
            ;(el as HTMLElement).style.filter = ''
            el.removeAttribute(attr)
          })
        },
        { cls: MARK_CLASS, attr: BLUR_ATTR },
      )
    },
  }
}
