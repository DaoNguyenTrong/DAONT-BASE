import { config, enableAutoUnmount } from '@vue/test-utils'
import { beforeAll, afterAll, afterEach, beforeEach } from 'vitest'
import i18n from '@/plugins/i18n'
import { setConfirmationOverrideNaive } from '@/lib/confirmation-override'
import { server } from './helpers/msw/server'

beforeAll(() => {
  server.listen({ onUnhandledRequest: 'error' })
})

afterEach(() => {
  server.resetHandlers()
})

afterAll(() => {
  server.close()
})

// Unmount every wrapper after each test so component-owned timers/intervals/listeners (registered
// in onMounted, cleared in onUnmounted) never outlive the test that created them and fire into the
// next one — or after teardown, failing the run with an unhandled error.
enableAutoUnmount(afterEach)

config.global.plugins = [i18n]

setConfirmationOverrideNaive((options) => {
  void options.accept?.()
})

beforeEach(() => {
  localStorage.clear()
  i18n.global.locale.value = 'en'
})
