import type { Component } from 'vue'
import type { DialogReactive } from 'naive-ui'
import { useDialog } from 'naive-ui'

/**
 * Naive UI equivalent of `useAppDialog` (src/composables/use-app-dialog.ts).
 *
 * Mounts an arbitrary component as a Naive UI dialog's `content`. Uses the
 * CONTEXT-based `useDialog()` (backed by the `<n-dialog-provider>` already
 * wrapping the app in App.vue), NOT the discrete `dialog` from
 * `src/lib/feedback-naive.ts` — the discrete API mounts into a fully separate
 * Vue app instance with no plugins installed (no i18n, no Pinia), so any
 * dialog CONTENT component that calls `useI18n()`/a Pinia store/etc would
 * throw during setup. Context-based `useDialog()` mounts within the main
 * app's tree instead, inheriting all of it. (feedback-naive.ts's discrete
 * `dialog`/`message` stay as-is — they only ever render pre-resolved plain
 * strings passed in by the caller, so they never hit this problem.)
 *
 * Naive UI's dialog API has no `templates.footer` / `DynamicDialogInstance`
 * equivalent, so the confirm/cancel footer is replicated via the dialog's
 * built-in `positiveText` / `negativeText` + `onPositiveClick` /
 * `onNegativeClick` props instead of a custom footer component. The
 * loading-guard intent of the original's `_loading` / `_onConfirm` (block
 * double-submit while `onConfirm` is pending) is preserved via the `loading`
 * ref below and Naive's own `loading` dialog prop.
 */
interface AppDialogOptionsNaive<T = Record<string, unknown>> {
  header: string
  data?: T
  dialogClass?: string
  confirmLabel?: string
  cancelLabel?: string
  onConfirm: (close: () => void) => Promise<void> | void
  onCancel?: () => void
}

export function useAppDialogNaive() {
  const { t } = useI18n()
  const dialog = useDialog()

  function open<T = Record<string, unknown>>(
    component: Component,
    options: AppDialogOptionsNaive<T>,
  ): DialogReactive {
    const loading = ref(false)
    const rawComponent = markRaw(component)

    const close = () => {
      instance.destroy()
    }

    // Also exposed as a prop so the rendered component can close itself
    // (e.g. a secondary action inside the dialog, independent of the main
    // confirm button) — mirrors the old useAppDialog's dialogRef.value.close().
    const props = markRaw({ ...(options.data ?? {}), close })

    const instance: DialogReactive = dialog.create({
      title: options.header,
      class: options.dialogClass,
      positiveText: options.confirmLabel ?? t('common.save'),
      negativeText: options.cancelLabel ?? t('common.cancel'),
      // Naive's NDialog hardcodes its action buttons to size="small" (28px) —
      // too small next to this dialog's enlarged padding/radius. The button
      // size can't be reached via themeOverrides (it's a literal prop, not a
      // theme var), but positiveButtonProps/negativeButtonProps are spread
      // onto the button AFTER that hardcoded size, so they win.
      positiveButtonProps: { size: 'large' },
      negativeButtonProps: { size: 'large' },
      // Mask click stays disabled (accidental outside-click shouldn't discard
      // form input), but Esc is a deliberate, low-risk dismiss gesture users
      // expect to work — same as the header's X close button.
      maskClosable: false,
      closeOnEsc: true,
      content: () => h(rawComponent, props),
      onPositiveClick: async () => {
        // Guard against double-submit while a previous onConfirm is pending —
        // mirrors the original's `if (loading.value) return` in `_onConfirm`.
        if (loading.value) return false
        loading.value = true
        instance.loading = true
        try {
          // Closing is driven entirely by `onConfirm` calling `close()` (or
          // not), same as the original passing `close` into `onConfirm` —
          // so we always return `false` here to stop Naive's own auto-hide.
          await options.onConfirm(close)
        } finally {
          loading.value = false
          instance.loading = false
        }
        return false
      },
      onNegativeClick: () => {
        options.onCancel?.()
      },
      onClose: () => {
        options.onCancel?.()
      },
    })

    return instance
  }

  return { open }
}
