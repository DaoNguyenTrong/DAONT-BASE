/**
 * Stand-in for a lazy route view in tests that exercise the router's guards and route table, not the
 * views. Loading a real view chunk (and everything it imports) on the first navigation is slow, and
 * on a busy machine can push the test past its timeout.
 *
 * `vi.mock` is hoisted per file, so each test file lists the views it navigates to:
 * `vi.mock('@/views/LoginView.vue', () => stubView())`.
 */
export function stubView() {
  return { default: { render: () => null } }
}
