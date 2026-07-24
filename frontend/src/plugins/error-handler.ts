import type { App } from 'vue'

export function setupErrorHandlers(app: App) {
  app.config.errorHandler = (err, _instance, info) => {
    console.error('[Vue Error]', info, err)
    if (err instanceof ApiError) {
      showErrorMessage(err.problem.title, err.problem.detail)
    } else if (err instanceof Error) {
      showErrorMessage(err.message)
    }
  }

  window.addEventListener('unhandledrejection', (event) => {
    const err = event.reason
    if (err instanceof ApiError) {
      event.preventDefault()
      showErrorMessage(err.problem.title, err.problem.detail)
    } else if (err instanceof Error) {
      event.preventDefault()
      showErrorMessage(err.message)
    }
  })
}
