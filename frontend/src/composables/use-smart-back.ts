import type { RouteLocationRaw } from 'vue-router'

export function useSmartBack() {
  const router = useRouter()

  function back(fallback: RouteLocationRaw) {
    if (window.history.state?.back) {
      router.back()
    } else {
      router.push(fallback)
    }
  }

  return { back }
}
