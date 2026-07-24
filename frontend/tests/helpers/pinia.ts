import { createTestingPinia } from '@pinia/testing'
import { setActivePinia } from 'pinia'
import { vi } from 'vitest'

export function setupTestPinia() {
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false })
  setActivePinia(pinia)
  return pinia
}
