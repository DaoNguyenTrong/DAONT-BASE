import { createTestingPinia } from '@pinia/testing'
import { setActivePinia, type Pinia } from 'pinia'
import { vi } from 'vitest'

export function setupTestPinia() {
  const pinia = createTestingPinia({ createSpy: vi.fn, stubActions: false })
  setActivePinia(pinia)
  return pinia
}

/**
 * Reads a store from the given (mounted) `pinia`. Not `useXxxStore(pinia)`: with NODE_ENV=test and a
 * testing pinia active, Pinia ignores that argument and returns the store of the *active* pinia —
 * which a previous test's still-pending timer or request may have switched to its own instance.
 */
export function storeIn<S>(pinia: Pinia, useStore: () => S): S {
  setActivePinia(pinia)
  return useStore()
}
