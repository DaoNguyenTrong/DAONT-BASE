/**
 * Stand-in for a lazy route view in router tests: loading a real view chunk can push a test past
 * its timeout. `vi.mock` is hoisted per file, so each file mocks the views it navigates to.
 */
export function stubView() {
  return { default: { render: () => null } }
}
