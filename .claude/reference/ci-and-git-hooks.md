# CI and Git Hooks

Reference, not a rule: no `paths:` frontmatter, so it is not auto-loaded. Read it when changing CI workflows, Lefthook config, or git hooks.

## Git Hooks

Managed by [Lefthook](https://lefthook.dev) — config in root `lefthook.yml`, not `.husky/`.

```bash
# Run once at the repo root (not --cwd frontend) — installs git hooks (prepare script: lefthook install)
bun install
```

- **pre-commit**: `piped: true` — stops at the first failing command instead of running the rest. Blocks direct commits to `main` (bypass: `git commit --no-verify` — needed by nothing in the documented workflow except tooling that intentionally commits there); blocks staged merge-conflict markers; runs `secretlint` on all staged files (known secret *formats* only — private keys, GitHub/Slack/npm tokens; does not catch arbitrary custom secrets); runs `prettier --write` on staged `frontend/src/**`, `frontend/e2e/**` and `frontend/user-guide/tools/**` files (`format` and CI's `prettier --check` cover only the first two); `prettier-guide` does the same for staged `frontend/user-guide/site/**` files, run from that folder (`root:`) so `bunx` uses the guide's pinned prettier. Last, `comments` runs `scripts/check-comments.ts --staged` over the staged blobs (`comments.md` — no comment block over 3 lines or comment line over 120 characters). No backend formatting hook: C# style rules live in the root `.editorconfig`, and the ones set to `error` (`var` → IDE0008) fail `dotnet build` through `EnforceCodeStyleInBuild`, so CI's `backend` job catches them.
- **commit-msg**: commitlint, Conventional Commits (`@commitlint/config-conventional`).

Note: `release/*` is intentionally **not** blocked by the branch guard — the `git-release` skill (from the `release-kit` plugin; bindings in `.claude/release-kit.json`) cuts `release/vX.Y.Z` and commits the CHANGELOG section (and any version-file bump) on it (Phase 1, again in Phase 2), then opens the release PR from `release/vX.Y.Z` to `main` once QA stabilization is done. `dev` isn't blocked either, but nothing is committed to it directly: with `devWrites: "pr"` quick release's CHANGELOG and the post-release reconcile are PRs (`docs/process/lifecycle.md` § Phát hành, ổn định và hotfix). See `CONTRIBUTING.md` § Release Process.

Glob gotcha if you edit `lefthook.yml`: Lefthook's glob matcher (`gobwas/glob`) treats `dir/**/*` as requiring **at least one** intermediate directory — it silently skips files directly in `dir/` (e.g. `frontend/src/main.ts` under a `frontend/src/**/*` glob). Verified empirically (not from docs) while wiring this up. Match both depths with two patterns (see `prettier`'s glob list in `lefthook.yml`), or use a bare `**` when there's no path prefix to anchor (see `secretlint`'s glob).

## CI

The test gate lives in one reusable workflow, `.github/workflows/test.yml`, run by two callers:

- **`ci.yml`** — every PR into `dev`, `main` or `release/**`, and every push to `dev` (the merged result of each PR, re-checked).
- **`release.yml`** — the `v*` tag, before any image is built.

Four parallel jobs. `comments`: `bun test scripts/` and `scripts/check-comments.ts` over the whole tracked tree — no input, so it runs on every change, docs-only included. `guide`: the user guide's `format:check`, `test` and `build`. `backend`: `test.sh`, then the OpenAPI export followed by `git diff --exit-code shared/openapi/openapi.json` — a controller/DTO change committed without a re-export fails here. `frontend`: `bun install --frozen-lockfile`, `prettier --check` (catches commits that skipped Lefthook), `build-only` (writes the gitignored `src/typings/` — `bun install` doesn't, and a test run writes only the components it touched), `type-check` (vitest doesn't type-check), `lint:boundaries` (import-boundary rules, `eslint.config.js`), `test:coverage`. Both jobs write a coverage table to the run summary — reported, not gated (`testing.md` § Coverage). Run them locally before pushing with `scripts/test-gate.sh` — the same checks minus coverage, and the gate `git-release` runs (`release-kit.json`); change it together with `test.yml`; a red check on `main` blocks the merge once `test / comments`, `test / backend`, `test / frontend` and `test / guide` are required status checks (see `CONTRIBUTING.md`). E2E (Playwright) is not in CI — it needs a running backend with seeded accounts.

`.gitlab-ci.yml` mirrors this: the `test:backend`, `test:frontend`, `test:guide` and `test:comments` jobs run on merge requests, pushes to `dev` and `v*` tags; `images`/`release` only on tags. Change the GitHub and GitLab files together.

The release pipeline, fired by the `v*` tag the `git-release` skill (from the `release-kit` plugin) pushes: test → build and push both production images → create the release. `.github/workflows/release.yml` (images to GHCR) and `.gitlab-ci.yml` (images to the project's Container Registry) are the same pipeline for the two hosts; change both together. Images are built from the tag's pipeline, never from a push to `main`: `git-release` merges into `main` before it tags, so a `main` build can't see the tag and MinVer/`git describe` would report `X.Y.(Z+1)-preview`. Both need full history (`fetch-depth: 0` / `GIT_DEPTH: "0"`), and `v*` tags must be protected.
