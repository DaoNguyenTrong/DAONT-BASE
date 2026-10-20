import type { Component } from 'vue'
import type { DialogReactive } from 'naive-ui'
import { useDialog } from 'naive-ui'

/**
 * Mounts a component as dialog content via context-based `useDialog()`, not the discrete `dialog`
 * in lib/feedback-naive.ts: that mounts a separate Vue app with no i18n/Pinia, so content calling
 * `useI18n()` or a store would throw during setup.
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

// Optional hook a content component can `defineExpose`: `open()` awaits its `FormInst`-shaped
// `validate()` before `onConfirm`, so an invalid `<n-form>` blocks the confirm.
interface AppDialogContentExpose {
  validate?: () => Promise<unknown>
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
    const contentRef = ref<AppDialogContentExpose | null>(null)

    const close = () => {
      instance.destroy()
    }

    // Also passed as a prop so the content can close itself (e.g. a secondary action inside the
    // dialog, independent of the confirm button).
    const props = markRaw({ ...(options.data ?? {}), close })

    const instance: DialogReactive = dialog.create({
      title: options.header,
      class: options.dialogClass,
      positiveText: options.confirmLabel ?? t('common.save'),
      negativeText: options.cancelLabel ?? t('common.cancel'),
      // NDialog hardcodes size="small" on its action buttons — a literal prop, not a theme var,
      // too small for this dialog's padding. These props are spread after it, so they win.
      positiveButtonProps: { size: 'large' },
      negativeButtonProps: { size: 'large' },
      // Mask click stays disabled (accidental outside-click shouldn't discard
      // form input), but Esc is a deliberate, low-risk dismiss gesture users
      // expect to work — same as the header's X close button.
      maskClosable: false,
      closeOnEsc: true,
      content: () => h(rawComponent, { ...props, ref: contentRef }),
      onPositiveClick: async () => {
        // Guard against double-submit while a previous onConfirm is pending.
        if (loading.value) return false

        if (contentRef.value?.validate) {
          try {
            await contentRef.value.validate()
          } catch {
            return false
          }
        }

        loading.value = true
        instance.loading = true
        try {
          // Closing is driven entirely by `onConfirm` calling `close()` (or not), so always
          // return `false` here to stop Naive's own auto-hide.
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
