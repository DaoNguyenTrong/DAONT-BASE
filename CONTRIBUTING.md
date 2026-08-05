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
| `release/vX.Y.Z` | QA stabilization branch for a release — cut from `dev`, merges only into `main`, does **not** back-merge to `dev` |
| `dev` | Integration/staging |
| `feature/*` | New features, branched from `dev` |
| `fix/*` | Bug fixes — from `dev` for in-progress work, or from `release/vX.Y.Z` for bugs QA finds during stabilization |
| `test/*` | Optional — for large/independent e2e specs added on `release/vX.Y.Z` that need separate review |
| `hotfix/*` | Emergency production fixes — from `main`, merges to `main`, then back-merges into `dev` |

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

Then create PR to `dev` branch on GitHub.

### 4. Update CHANGELOG

When your PR is merged, add entry to `CHANGELOG.md` under `[Unreleased]`:

```markdown
## [Unreleased]
### Added
- Your new feature description
```

## Release Process

Two phases, handled by the `git-release` skill (`/git-release`) — cutting a release and shipping it are separate steps, since QA stabilization on `release/vX.Y.Z` happens in between and can take any amount of time.

### Phase 1 — Cut the release branch (on `dev`)

1. Ensure all features for this release are merged to `dev`.
2. Finalize `CHANGELOG.md` on `dev`: rename `## [Unreleased]` to `## [vX.Y.Z] - YYYY-MM-DD`, add a fresh empty `## [Unreleased]` above it. Commit and push to `dev`.
   **This entry is never edited again on any branch** — the later `release/vX.Y.Z → main` merge relies on that to stay conflict-free.
3. Cut `release/vX.Y.Z` from `dev` and push it.

QA stabilizes on `release/vX.Y.Z` from here. Bugs found during stabilization go through `fix/*` branched off `release/vX.Y.Z` and merged back into it via PR.

### Phase 2 — Ship (on `release/vX.Y.Z`)

4. Once QA signs off, run the full test suite on `release/vX.Y.Z` — this is the mandatory gate (a `fix/*` PR may have landed since the cut, so this can't be skipped even if Phase 1 already tested clean).
5. Create PR: `release/vX.Y.Z` → `main`. After merge, tag `origin/main` (never the release branch):

```bash
git checkout main
git pull origin main
git tag v1.x.0 -m "Release 1.x.0"
git push origin v1.x.0
```

6. GitHub Actions creates the release automatically from the tag.

`release/vX.Y.Z` is **not** back-merged into `dev` afterward — see the branch table above.

## Hotfix Process

For an emergency production fix that can't wait for `dev`'s next `[Unreleased]` to ship:

1. Branch `hotfix/vX.Y.Z` from `main`, fix, test, commit.
2. Update `CHANGELOG.md` on the hotfix branch: insert the new `## [vX.Y.Z]` section directly above the previous release entry. **Never** touch `dev`'s `[Unreleased]` section from a hotfix branch.
3. PR `hotfix/vX.Y.Z` → `main`, merge, tag `origin/main` the same way as a standard release.
4. Back-merge `main` into `dev` so the fix isn't lost on the next regular release — conflicts here are resolved manually, never auto-resolved.

## Versioning (SemVer)

- **MAJOR** (v2.0.0): Breaking changes
- **MINOR** (v1.1.0): New features, backward compatible
- **PATCH** (v1.0.1): Bug fixes

Version is managed automatically by MinVer based on git tags.
