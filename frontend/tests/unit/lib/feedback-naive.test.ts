import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import { nextTick } from 'vue'
import {
  dialog,
  requestConfirmationNaive,
  setConfirmationOverrideNaive,
  showErrorMessageNaive,
  showInfoMessageNaive,
  showSuccessMessageNaive,
} from '@/lib/feedback-naive'

// `message` isn't exported (only `dialog` is, for composable reuse), and
// `createDiscreteApi` runs once at module load — before any per-file `vi.mock`
// could intercept it, since `tests/setup.ts` already imports this module for
// `setConfirmationOverrideNaive`. So message assertions go through the real,
// teleported DOM output instead of a mock.
async function flushMessage() {
  await nextTick()
  await new Promise((resolve) => setTimeout(resolve, 20))
}

describe('feedback-naive', () => {
  beforeEach(() => {
    // tests/setup.ts installs a global auto-accept override for every other
    // test file — reset it here so we can exercise the real dialog.warning path.
    setConfirmationOverrideNaive(null)
  })

  afterEach(() => {
    vi.restoreAllMocks()
    // Restore the auto-accept override so later test files aren't affected.
    setConfirmationOverrideNaive((options) => {
      void options.accept?.()
    })
  })

  describe('requestConfirmationNaive', () => {
    it('calls the override instead of dialog.warning when an override is set', () => {
      const warningSpy = vi.spyOn(dialog, 'warning').mockImplementation(() => ({}) as never)
      const override = vi.fn()
      setConfirmationOverrideNaive(override)

      requestConfirmationNaive({ header: 'Delete?', message: 'Sure?' })

      expect(override).toHaveBeenCalledWith({ header: 'Delete?', message: 'Sure?' })
      expect(warningSpy).not.toHaveBeenCalled()
    })

    it('calls dialog.warning with mapped title/content when no override is set', () => {
      const warningSpy = vi.spyOn(dialog, 'warning').mockImplementation(() => ({}) as never)

      requestConfirmationNaive({ header: 'Delete?', message: 'This is permanent' })

      expect(warningSpy).toHaveBeenCalledTimes(1)
      const options = warningSpy.mock.calls[0]![0]
      expect(options.title).toBe('Delete?')
      expect(options.content).toBe('This is permanent')
    })

    it('defaults positiveText/negativeText to Yes/No when labels are omitted', () => {
      const warningSpy = vi.spyOn(dialog, 'warning').mockImplementation(() => ({}) as never)

      requestConfirmationNaive({ header: 'Delete?', message: 'Sure?' })

      const options = warningSpy.mock.calls[0]![0]
      expect(options.positiveText).toBe('Yes')
      expect(options.negativeText).toBe('No')
    })

    it('uses acceptLabel/rejectLabel when provided', () => {
      const warningSpy = vi.spyOn(dialog, 'warning').mockImplementation(() => ({}) as never)

      requestConfirmationNaive({
        header: 'Delete?',
        message: 'Sure?',
        acceptLabel: 'Delete',
        rejectLabel: 'Cancel',
      })

      const options = warningSpy.mock.calls[0]![0]
      expect(options.positiveText).toBe('Delete')
      expect(options.negativeText).toBe('Cancel')
    })

    it('calls accept when onPositiveClick fires', () => {
      const warningSpy = vi.spyOn(dialog, 'warning').mockImplementation(() => ({}) as never)
      const accept = vi.fn()

      requestConfirmationNaive({ header: 'Delete?', message: 'Sure?', accept })

      const options = warningSpy.mock.calls[0]![0]
      options.onPositiveClick?.(new MouseEvent('click'))

      expect(accept).toHaveBeenCalledTimes(1)
    })

    it('calls reject when onNegativeClick fires', () => {
      const warningSpy = vi.spyOn(dialog, 'warning').mockImplementation(() => ({}) as never)
      const reject = vi.fn()

      requestConfirmationNaive({ header: 'Delete?', message: 'Sure?', reject })

      const options = warningSpy.mock.calls[0]![0]
      options.onNegativeClick?.(new MouseEvent('click'))

      expect(reject).toHaveBeenCalledTimes(1)
    })
  })

  describe('showSuccessMessageNaive', () => {
    it('shows the summary only when no detail is given', async () => {
      showSuccessMessageNaive('AloneSummary123')
      await flushMessage()
      expect(document.body.innerHTML).toContain('AloneSummary123')
    })

    it('joins summary and detail with a newline when detail is given', async () => {
      showSuccessMessageNaive('SummaryPart456', 'DetailPart456')
      await flushMessage()
      expect(document.body.innerHTML).toContain('SummaryPart456')
      expect(document.body.innerHTML).toContain('DetailPart456')
    })
  })

  describe('showErrorMessageNaive', () => {
    it('renders through message.error with the same formatting rules', async () => {
      showErrorMessageNaive('ErrorSummary789', 'ErrorDetail789')
      await flushMessage()
      expect(document.body.innerHTML).toContain('ErrorSummary789')
      expect(document.body.innerHTML).toContain('ErrorDetail789')
    })
  })

  describe('showInfoMessageNaive', () => {
    it('renders through message.info with the same formatting rules', async () => {
      showInfoMessageNaive('InfoSummary000')
      await flushMessage()
      expect(document.body.innerHTML).toContain('InfoSummary000')
    })
  })
})
