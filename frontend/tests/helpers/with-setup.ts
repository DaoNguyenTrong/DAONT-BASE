import { createApp, type App } from 'vue'
import { createRouter, createMemoryHistory, type Router, type RouteRecordRaw } from 'vue-router'
import i18n from '@/plugins/i18n'

const DEFAULT_ROUTES: RouteRecordRaw[] = [
  { path: '/list', name: 'list', component: { template: '<div />' } },
]

export async function withSetup<T>(
  composable: () => T,
  initialPath = '/list',
  routes: RouteRecordRaw[] = DEFAULT_ROUTES,
): Promise<{ result: T; app: App; router: Router }> {
  const router = createRouter({
    history: createMemoryHistory(),
    routes,
  })

  let result!: T
  const app = createApp({
    setup() {
      result = composable()
      return () => null
    },
  })

  app.use(router)
  app.use(i18n)
  await router.push(initialPath)
  await router.isReady()
  app.mount(document.createElement('div'))

  return { result, app, router }
}
