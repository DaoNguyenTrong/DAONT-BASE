export type ConfirmationOptionsNaive = {
  header?: string
  message?: string
  acceptLabel?: string
  rejectLabel?: string
  accept?: () => void
  reject?: () => void
}

// Test-only seam, mirroring `setConfirmationService` in feedback.ts — the discrete dialog API has no
// "not yet installed" state to hook into, so tests need an explicit override instead to avoid
// depending on a dialog that's teleported outside the mounted component tree. Kept apart from
// `feedback-naive.ts` so `tests/setup.ts` can install it without importing naive-ui (and its
// `createDiscreteApi` side effect) into every test file.
let confirmOverride: ((options: ConfirmationOptionsNaive) => void) | null = null

export function setConfirmationOverrideNaive(
  fn: ((options: ConfirmationOptionsNaive) => void) | null,
) {
  confirmOverride = fn
}

export function getConfirmationOverrideNaive() {
  return confirmOverride
}
