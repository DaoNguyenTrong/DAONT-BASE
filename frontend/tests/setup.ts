import { config } from '@vue/test-utils'
import { beforeAll, afterAll, afterEach, beforeEach } from 'vitest'
import i18n from '@/plugins/i18n'
import { setConfirmationOverrideNaive } from '@/lib/feedback-naive'
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

config.global.plugins = [i18n]

setConfirmationOverrideNaive((options) => {
  void options.accept?.()
})

beforeEach(() => {
  localStorage.clear()
  i18n.global.locale.value = 'en'
})
