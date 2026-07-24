---
name: git-release
description: 'Release a new version: update CHANGELOG, create PR from dev to main, merge, tag, and push. Supports standard release (dev → main) and hotfix (main → hotfix branch → main + back-merge to dev). Uses MinVer — version is derived from git tags. Examples: "Release v1.1.0", "Release patch", "Hotfix v1.2.1"'
---

# Git Release

Automates the release workflow with CHANGELOG and git tag. This is a .NET project using **MinVer** — version is derived from git tags, there is no version file to bump.

Two modes:
- **Standard release**: `dev → main` for planned releases
- **Hotfix**: `main → hotfix/vX.Y.Z → main`, then back-merge to `dev`

---

## Standard Release Workflow (dev → main)

### 1. Determine version

- User provides a version (e.g. `v1.2.0`) → use it.
- User says `major` / `minor` / `patch` → bump from latest git tag.
- No version given → read latest tag, suggest next minor. Ask to confirm.

### 2. Pre-flight checks

```bash
git branch --show-current                 # must be "dev"
git fetch origin
git status --porcelain                    # must be clean
git log --oneline origin/dev..dev         # must be empty (no unpushed commits)
git tag --sort=-v:refname | head -1       # last release tag
```

**Stop and warn** if not on `dev`, working directory is dirty, or dev has unpushed commits.

### 3. Finalize CHANGELOG.md

The `git-commit` skill already adds entries under `## [Unreleased]`. This step only promotes that section to the release version.

1. Replace `## [Unreleased]` with `## [vX.Y.Z] - YYYY-MM-DD`
2. Add a new empty `## [Unreleased]` section above it

Before:

```markdown
## [Unreleased]

### Added

- some feature
```

After:

```markdown
## [Unreleased]

## [vX.Y.Z] - YYYY-MM-DD

### Added

- some feature
```

**Stop and warn** if `## [Unreleased]` has no entries — there is nothing to release.

Commit and push:

```bash
git checkout dev
git add CHANGELOG.md
git commit -m "docs: update CHANGELOG for vX.Y.Z"
git push origin dev
```

### 4. Create and merge release PR

```bash
gh pr create --base main --head dev \
  --title "Release vX.Y.Z" \
  --body "<changelog entries from step 3>"
gh pr merge <number> --merge --subject "Release vX.Y.Z"
```

### 5. Tag on main and push

MinVer derives the version from git tags. Always tag `origin/main` after merge — never tag `dev`.

```bash
git fetch origin
git tag vX.Y.Z origin/main
git push origin vX.Y.Z
```

### 6. Summary

Print:

- Release version
- PR link
- Tag name
- Number of commits included

---

## Hotfix Workflow (main → hotfix/vX.Y.Z → main → dev)

Use when a critical bug must be fixed on production without including unreleased changes from `dev`.

### 1. Determine version

Hotfix always bumps **patch** from the latest tag (e.g. `v1.2.0` → `v1.2.1`).

- User provides a version → use it.
- No version given → read latest tag, bump patch. Ask to confirm.

### 2. Pre-flight checks

```bash
git fetch origin
git status --porcelain                    # must be clean
git tag --sort=-v:refname | head -1       # last release tag
```

**Stop and warn** if working directory is dirty.

### 3. Create hotfix branch from main

Check current branch first:

```bash
git branch --show-current
```

**If already on `hotfix/vX.Y.Z`** — skip branch creation, continue to step 4.

**If on a different `hotfix/*` branch** — stop and warn: another hotfix is in progress. Ask the user to confirm the version before continuing.

**Otherwise** — create the branch:

```bash
git checkout -b hotfix/vX.Y.Z origin/main
git push origin hotfix/vX.Y.Z
```

**Stop here.** Ask the user to apply and commit the fix on this branch, then confirm when ready to continue.

Once the user confirms, push:

```bash
git push origin hotfix/vX.Y.Z
```

### 4. Update CHANGELOG.md on hotfix branch

Unlike standard release, `[Unreleased]` on `dev` must NOT be touched. Instead, insert a new release section directly above the previous release:

Before:

```markdown
## [Unreleased]

## [v1.2.0] - 2026-05-01
```

After:

```markdown
## [Unreleased]

## [v1.2.1] - YYYY-MM-DD

### Fixed

- <describe the hotfix>

## [v1.2.0] - 2026-05-01
```

Commit and push on the hotfix branch:

```bash
git add CHANGELOG.md
git commit -m "docs: update CHANGELOG for vX.Y.Z"
git push origin hotfix/vX.Y.Z
```

### 5. Merge hotfix into main

```bash
gh pr create --base main --head hotfix/vX.Y.Z \
  --title "Hotfix vX.Y.Z" \
  --body "<changelog entries from step 4>"
gh pr merge <number> --merge --subject "Hotfix vX.Y.Z"
```

### 6. Tag on main and push

```bash
git fetch origin
git tag vX.Y.Z origin/main
git push origin vX.Y.Z
```

### 7. Back-merge main into dev

Keeps `dev` in sync — prevents the fix from being lost in the next standard release.

Merge from `main` (not the hotfix branch) — `main` is the source of truth after tagging, and avoids divergence if the hotfix PR was squash-merged.

```bash
git fetch origin
git checkout dev
git merge origin/main --no-ff -m "chore: back-merge hotfix vX.Y.Z into dev"
git push origin dev
```

If there are merge conflicts, stop and report — do not resolve automatically.

### 8. Summary

Print:

- Hotfix version
- PR to main (link)
- Tag name
- Back-merge commit on dev

---

## Rules

- Never force push.
- Never skip the CHANGELOG finalization.
- No version file to bump — MinVer reads the git tag directly.
- Always tag `origin/main` after merge, never `dev` or the hotfix branch.
- Hotfix: never modify `[Unreleased]` section — it belongs to `dev`.
- Hotfix: always back-merge into `dev` after tagging.
- If any step fails, stop and report — do not continue.
