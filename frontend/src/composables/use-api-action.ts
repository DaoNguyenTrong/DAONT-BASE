interface UseApiActionOptions {
  onCode?: Record<string, (detail: string | undefined) => void>
  fallbackMessage?: string
}

export function useApiAction() {
  const isLoading = ref(false)
  const error = ref<string | null>(null)
  const fieldErrors = ref<Record<string, string>>({})

  async function run<T>(
    action: () => Promise<T>,
    options: UseApiActionOptions = {},
  ): Promise<T | undefined> {
    if (isLoading.value) {
      return undefined
    }

    isLoading.value = true
    error.value = null
    fieldErrors.value = {}

    try {
      return await action()
    } catch (caught) {
      if (caught instanceof ApiError) {
        const code = caught.problem.code
        const handler = code ? options.onCode?.[code] : undefined

        if (code === 'ValidationFailed') {
          fieldErrors.value = mapValidationErrors(caught.problem.errors)
        } else if (handler) {
          handler(caught.problem.detail)
        } else {
          error.value =
            caught.problem.detail ??
            options.fallbackMessage ??
            i18n.global.t('errors.requestFailed')
        }
      } else {
        error.value = options.fallbackMessage ?? i18n.global.t('errors.requestFailed')
      }

      return undefined
    } finally {
      isLoading.value = false
    }
  }

  function clearFieldError(field: string) {
    if (!(field in fieldErrors.value)) {
      return
    }

    const next = { ...fieldErrors.value }
    delete next[field]
    fieldErrors.value = next
  }

  return { isLoading, error, fieldErrors, run, clearFieldError }
}
