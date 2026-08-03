// Matches the commit types documented in CONTRIBUTING.md (feat/fix/docs/refactor/test/chore) —
// @commitlint/config-conventional's type list is a superset (adds style/perf/build/ci/revert),
// which is harmless rather than restricting to exactly CONTRIBUTING.md's six.
module.exports = {
  extends: ['@commitlint/config-conventional']
}
