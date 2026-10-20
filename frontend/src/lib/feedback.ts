import {
  requestConfirmationNaive,
  showErrorMessageNaive,
  showInfoMessageNaive,
  showSuccessMessageNaive,
} from './feedback-naive'

type ToastSeverity = 'success' | 'info' | 'warn' | 'error'

type ConfirmationOptions = {
  header?: string
  message?: string
  acceptLabel?: string
  rejectLabel?: string
  accept?: () => void
  reject?: () => void
}

type NotificationOptions = {
  severity?: ToastSeverity
  summary: string
  detail?: string
  life?: number
}

/**
 * The stable feedback API the app's call sites use; delegates to feedback-naive.ts.
 */
export function requestConfirmation(options: ConfirmationOptions) {
  requestConfirmationNaive({
    header: options.header,
    message: options.message,
    acceptLabel: options.acceptLabel,
    rejectLabel: options.rejectLabel,
    accept: options.accept,
    reject: options.reject,
  })
}

export function showNotification({
  severity = 'info',
  summary,
  detail,
  life,
}: NotificationOptions) {
  if (severity === 'success') showSuccessMessageNaive(summary, detail, life)
  else if (severity === 'error') showErrorMessageNaive(summary, detail, life)
  else showInfoMessageNaive(summary, detail, life)
}

export function showSuccessMessage(summary: string, detail?: string, life?: number) {
  showSuccessMessageNaive(summary, detail, life)
}

export function showErrorMessage(summary: string, detail?: string, life?: number) {
  showErrorMessageNaive(summary, detail, life)
}

export function showInfoMessage(summary: string, detail?: string, life?: number) {
  showInfoMessageNaive(summary, detail, life)
}
