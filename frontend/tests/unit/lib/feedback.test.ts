import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest'
import * as feedbackNaive from '@/lib/feedback-naive'
import {
  requestConfirmation,
  showErrorMessage,
  showInfoMessage,
  showNotification,
  showSuccessMessage,
} from '@/lib/feedback'

beforeEach(() => {
  vi.spyOn(feedbackNaive, 'requestConfirmationNaive').mockImplementation(() => {})
  vi.spyOn(feedbackNaive, 'showSuccessMessageNaive').mockImplementation(() => {})
  vi.spyOn(feedbackNaive, 'showErrorMessageNaive').mockImplementation(() => {})
  vi.spyOn(feedbackNaive, 'showInfoMessageNaive').mockImplementation(() => {})
})

afterEach(() => {
  vi.restoreAllMocks()
})

describe('showNotification', () => {
  it("routes 'success' severity to the success message", () => {
    showNotification({ severity: 'success', summary: 'Saved', detail: 'ok', life: 1000 })
    expect(feedbackNaive.showSuccessMessageNaive).toHaveBeenCalledWith('Saved', 'ok', 1000)
  })

  it("routes 'error' severity to the error message", () => {
    showNotification({ severity: 'error', summary: 'Failed', detail: 'bad', life: 2000 })
    expect(feedbackNaive.showErrorMessageNaive).toHaveBeenCalledWith('Failed', 'bad', 2000)
  })

  it("routes 'warn' severity to the info message (no dedicated warn path)", () => {
    showNotification({ severity: 'warn', summary: 'Careful' })
    expect(feedbackNaive.showInfoMessageNaive).toHaveBeenCalledWith('Careful', undefined, undefined)
    expect(feedbackNaive.showSuccessMessageNaive).not.toHaveBeenCalled()
    expect(feedbackNaive.showErrorMessageNaive).not.toHaveBeenCalled()
  })

  it("routes 'info' severity to the info message", () => {
    showNotification({ severity: 'info', summary: 'FYI' })
    expect(feedbackNaive.showInfoMessageNaive).toHaveBeenCalledWith('FYI', undefined, undefined)
  })

  it('defaults to the info message when no severity is given', () => {
    showNotification({ summary: 'Default' })
    expect(feedbackNaive.showInfoMessageNaive).toHaveBeenCalledWith('Default', undefined, undefined)
  })
})

describe('requestConfirmation', () => {
  it('forwards all confirmation options 1:1', () => {
    const accept = vi.fn()
    const reject = vi.fn()
    requestConfirmation({
      header: 'Delete?',
      message: 'This is permanent',
      acceptLabel: 'Delete',
      rejectLabel: 'Cancel',
      accept,
      reject,
    })
    expect(feedbackNaive.requestConfirmationNaive).toHaveBeenCalledWith({
      header: 'Delete?',
      message: 'This is permanent',
      acceptLabel: 'Delete',
      rejectLabel: 'Cancel',
      accept,
      reject,
    })
  })
})

describe('message forwarders', () => {
  it('forwards showSuccessMessage args', () => {
    showSuccessMessage('a', 'b', 3)
    expect(feedbackNaive.showSuccessMessageNaive).toHaveBeenCalledWith('a', 'b', 3)
  })

  it('forwards showErrorMessage args', () => {
    showErrorMessage('a', 'b', 3)
    expect(feedbackNaive.showErrorMessageNaive).toHaveBeenCalledWith('a', 'b', 3)
  })

  it('forwards showInfoMessage args', () => {
    showInfoMessage('a', 'b', 3)
    expect(feedbackNaive.showInfoMessageNaive).toHaveBeenCalledWith('a', 'b', 3)
  })
})
