# Contributing to StarterKit

## Git Workflow

```
main ────────────────────────────────────● (production, tagged releases)
                                         /
release/vX.Y.Z ─────────●──●───●───────●   (QA stabilization)
                        /
dev ────────●────●────●───●────●────●────── (integration/staging)
             \       /
feature       ●────●
```

## Branches

| Branch | Purpose |
|--------|---------|
| `main` | Production, only receives merges via PR (no direct commits) |
| `release/vX.Y.Z` | QA stabilization branch for a release — cut from `dev`, merges into `main`; `main` is reconciled back into `dev` after the release ships |
| `dev` | Integration/staging |
| `feature/*` | New features, branched from `dev` |
| `fix/*` | Bug fixes — from `dev` for in-progress work, or from `release/vX.Y.Z` for bugs QA finds during stabilization |
| `test/*` | Optional — for large/independent e2e specs added on `release/vX.Y.Z` that need separate review |
| `hotfix/*` | Emergency production fixes — from `main`, merges to `main`; `main` is reconciled back into `dev` afterward |

## Development Flow

### 1. Start a Feature

```bash
git checkout dev
git pull origin dev
git checkout -b feature/your-feature-name
```

### 2. Develop & Commit

```bash
git add <files>
git commit -m "feat: add new feature"
```

Git hooks (see README's "Git Hooks" section for the one-time `bun install` needed at the repo root) enforce this automatically: pre-commit blocks direct commits to `main`, unresolved conflict markers, and known secret formats, and auto-formats staged `frontend/src/` files with Prettier; commit-msg rejects messages that don't follow the `feat:`/`fix:`/... convention above.

**Commit message format:**
- `feat:` - New feature
- `fix:` - Bug fix
- `docs:` - Documentation
- `refactor:` - Code refactoring
- `test:` - Adding tests
- `chore:` - Maintenance

### 3. Create Pull Request

```bash
git push origin feature/your-feature-name
```

Then create PR to `dev` branch on GitHub. CI (`.github/workflows/ci.yml`) runs the test gate on the PR — backend tests and an OpenAPI-export drift check when `backend/` or `shared/openapi/` changes, frontend formatting, type-check and unit tests when `frontend/` or `shared/openapi/` changes, and the user-guide checks when `frontend/` changes. A part the change doesn't touch reports as skipped, which counts as passing. A red check means fix it before review. `scripts/test-gate.sh` runs the same checks locally.

**Repository setup (once, by a maintainer):** add a ruleset for `main` that requires a pull request and the status checks `test / comments`, `test / backend`, `test / frontend` and `test / guide`. Requiring status checks on `dev` is fine too: nothing reaches `dev` without a PR — the release CHANGELOG is written on `release/vX.Y.Z`, quick release's CHANGELOG goes through a PR, and the post-release reconcile is a PR. On GitLab, enable *Settings → Merge requests → Pipelines must succeed*.

### 4. Changelog entry

Don't edit `CHANGELOG.md`. Put a `## Changelog` section in the PR description instead (the `release-kit:git-mr` format) — `/git-release` compiles the release's CHANGELOG from those sections:

```markdown
## Changelog
- Added: Users can complete the new workflow described in the PR.

## Verification
<checks run>
```

Write just `none` for a change no user would notice (refactor, tooling, tests, docs).

## Release Process

Two phases, handled by the `git-release` skill (`/git-release`, from the `release-kit` plugin — project bindings live in `.claude/release-kit.json`). Cutting a release and shipping it are separate steps, since QA stabilization on `release/vX.Y.Z` happens in between and can take any amount of time. Run `/git-release`; it detects the phase from the current branch.

### Phase 1 — Cut (on `dev`)

1. Ensure all features for this release are merged to `dev`.
2. `/git-release` on `dev` — the skill runs the fail-fast gate, cuts and pushes `release/vX.Y.Z`, then compiles the `## [vX.Y.Z] - YYYY-MM-DD` section **on the release branch** from the `## Changelog` sections of the PRs merged into `dev` since the last tag, shows you the draft, and commits it there. Nothing is committed to `dev`.

QA stabilizes on `release/vX.Y.Z` from here. Bugs found during stabilization go through `fix/*` branched off `release/vX.Y.Z` and PR'd back into it; `pr-reviewer` reviews them and you merge them (`docs/process/lifecycle.md` § Phát hành, ổn định và hotfix). Their `## Changelog` sections are added to the release section when you ship.

### Phase 2 — Ship (on `release/vX.Y.Z`)

3. `/git-release` on `release/vX.Y.Z` — the skill pulls any hotfix that reached `main` during stabilization, adds the fix PRs to the CHANGELOG section and sets the ship date, runs the mandatory test suite (a `fix/*` PR may have landed since the cut, so this can't be skipped), opens and merges the `release/vX.Y.Z` → `main` PR, then tags `origin/main`. The tag's pipeline (`.github/workflows/release.yml`, or `.gitlab-ci.yml` on GitLab) runs the tests, builds and pushes the production images versioned from the tag, then creates the release. Protect `v*` tags so only maintainers can push one.

### Phase 3 — Reconcile (on `dev`)

4. After shipping, merge `main` back into `dev` with a merge commit (never squash) and push it: `git checkout dev && git pull && git merge origin/main && git push`. `/git-release` only reminds you (`devWrites: "push"` in `.claude/release-kit.json`), so the stabilization fixes, the CHANGELOG section and the tag reach `dev`.

### Quick release (on `dev`)

For a small release that needs no stabilization window, `/git-release quick` on `dev` does it in one pass, the skill commits the CHANGELOG section straight to `dev`, runs the mandatory gate, opens and merges the `dev → main` PR after you confirm, tags `origin/main`, and reminds you to merge `main` back into `dev`. There is no `release/vX.Y.Z` branch, so nothing gets QA'd on its own first; use the two-phase release whenever QA has to sign off.

## Hotfix Process

For an emergency production fix that can't wait for `dev`'s next `[Unreleased]` to ship:

1. Branch `hotfix/<name>` from `main`; the fix is made by `implementer` and reviewed by `code-reviewer` on `origin/main...hotfix/<name>`.
2. `/git-release` on the hotfix branch handles the rest: it inserts the new `## [vX.Y.Z]` section directly above the previous release entry (**never** touching `dev`'s `[Unreleased]`), runs the mandatory test suite, PRs `hotfix/vX.Y.Z` → `main`, merges, and tags `origin/main`.
3. Reconcile: merge `main` into `dev` as in Phase 3 above; so the fix and its CHANGELOG section reach `dev`. Keep the hotfix's `## [vX.Y.Z]` section above the previous release; leave `dev`'s `## [Unreleased]` untouched.

## Versioning (SemVer)

- **MAJOR** (v2.0.0): Breaking changes
- **MINOR** (v1.1.0): New features, backward compatible
- **PATCH** (v1.0.1): Bug fixes

Version is managed automatically by MinVer based on git tags.
