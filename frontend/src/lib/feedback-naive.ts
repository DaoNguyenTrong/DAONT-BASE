import { createDiscreteApi, darkTheme } from 'naive-ui'
import type { DialogOptions } from 'naive-ui'
import naiveThemeOverrides from '@/theme/naive-theme'

/**
 * Naive UI equivalent of `src/lib/feedback.ts`.
 *
 * Naive UI's discrete API mounts its own isolated Vue app instance — it does
 * NOT inherit the main app's `<n-config-provider>` — so the theme must be
 * passed explicitly here. `configProviderProps` accepts a `Ref`
 * (`MaybeRef<ConfigProviderProps>`), so a computed tied to the same
 * `isDark` state App.vue uses keeps the discrete message/dialog instance in
 * sync with the app's light/dark toggle instead of freezing on light.
 */
const { isDark } = useThemePreference()

const configProviderProps = computed(() => ({
  theme: isDark.value ? darkTheme : null,
  themeOverrides: isDark.value ? naiveThemeOverrides.dark : naiveThemeOverrides.light,
}))

const { message, dialog } = createDiscreteApi(['message', 'dialog'], {
  configProviderProps,
})

// Exposed so other composables (e.g. `useAppDialogNaive`) can reuse the SAME
// discrete dialog instance instead of creating a second `createDiscreteApi` call.
export { dialog }

type ConfirmationOptionsNaive = {
  header?: string
  message?: string
  acceptLabel?: string
  rejectLabel?: string
  accept?: () => void
  reject?: () => void
}

// Test-only seam, mirroring `setConfirmationService` in feedback.ts — the
// discrete dialog API has no "not yet installed" state to hook into, so
// tests need an explicit override instead to avoid depending on a dialog
// that's teleported outside the mounted component tree.
let confirmOverride: ((options: ConfirmationOptionsNaive) => void) | null = null

export function setConfirmationOverrideNaive(
  fn: ((options: ConfirmationOptionsNaive) => void) | null,
) {
  confirmOverride = fn
}

export function requestConfirmationNaive(options: ConfirmationOptionsNaive) {
  if (confirmOverride) {
    confirmOverride(options)
    return
  }

  const dialogOptions: DialogOptions = {
    title: options.header,
    content: options.message,
    positiveText: options.acceptLabel ?? 'Yes',
    negativeText: options.rejectLabel ?? 'No',
    onPositiveClick: () => {
      void options.accept?.()
    },
    onNegativeClick: () => {
      void options.reject?.()
    },
  }

  dialog.warning(dialogOptions)
}

export function showSuccessMessageNaive(summary: string, detail?: string, life?: number) {
  message.success(detail ? `${summary}\n${detail}` : summary, { duration: life ?? 5000 })
}

export function showErrorMessageNaive(summary: string, detail?: string, life?: number) {
  message.error(detail ? `${summary}\n${detail}` : summary, { duration: life ?? 5000 })
}

export function showInfoMessageNaive(summary: string, detail?: string, life?: number) {
  message.info(detail ? `${summary}\n${detail}` : summary, { duration: life ?? 5000 })
}
