import type { Account, AuthResponse } from '@/api/types'

export const useAuthStore = defineStore('auth', () => {
  const account = ref<Account | null>(null)

  const isAuthenticated = computed(() => account.value !== null)

  function setAuth(response: AuthResponse) {
    account.value = response.account
  }

  function clearAuth() {
    account.value = null
  }

  return {
    account,
    isAuthenticated,
    setAuth,
    clearAuth,
  }
})
