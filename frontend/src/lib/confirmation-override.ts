export type ConfirmationOptionsNaive = {
  header?: string
  message?: string
  acceptLabel?: string
  rejectLabel?: string
  accept?: () => void
  reject?: () => void
}

// Test-only seam: tests can't hook the discrete dialog, which is teleported outside the mounted
// tree. Kept apart from `feedback-naive.ts` so `tests/setup.ts` can install it without importing
// naive-ui (and its `createDiscreteApi` side effect) into every test file.
let confirmOverride: ((options: ConfirmationOptionsNaive) => void) | null = null

export function setConfirmationOverrideNaive(
  fn: ((options: ConfirmationOptionsNaive) => void) | null,
) {
  confirmOverride = fn
}

export function getConfirmationOverrideNaive() {
  return confirmOverride
}
