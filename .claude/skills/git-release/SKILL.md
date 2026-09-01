---
name: git-release
description: 'Release a new version: cut a release/vX.Y.Z QA-stabilization branch from dev, finalize CHANGELOG, then (once stabilized) merge to main and tag. Supports standard release (dev → release/vX.Y.Z → main, two phases) and hotfix (main → hotfix/vX.Y.Z → main). Uses MinVer — version is derived from git tags. Examples: "Release v1.1.0", "Release patch", "Cut the release", "Ship the release", "Hotfix v1.2.1"'
---

# Git Release

Automates the release workflow with CHANGELOG and git tag. This is a .NET project using **MinVer** — version is derived from git tags, there is no version file to bump.

Two modes:

- **Standard release**: `dev → release/vX.Y.Z → main`. Two phases, run as separate skill invocations because QA stabilization happens in between and can take any amount of time — **Cut** (finalize CHANGELOG on `dev`, branch off) and **Ship** (test, merge to `main`, tag).
- **Hotfix**: `main → hotfix/vX.Y.Z → main`.

After a release or hotfix ships, the user reconciles `dev` with `main` themselves (merge `main` into `dev`) — this skill does not do it. The skill only reminds them in its final summary.

Scope: this skill versions the **backend only** (MinVer / git tag). `frontend/` has no independent version and is not bumped or tagged — it ships from the same tag.

Detect which mode/phase the user means from the current branch:

- On `dev` → **Standard release, Phase 1: Cut**.
- On `release/vX.Y.Z` → **Standard release, Phase 2: Ship**.
- On `hotfix/vX.Y.Z` or `main` with an explicit hotfix request → **Hotfix**.
- Ambiguous (e.g. on `main` with no hotfix context, or on an unrelated branch) → stop and ask which one the user means.

---

## Standard Release Workflow

### Phase 1: Cut the release branch (run on `dev`)

#### 1. Determine version

- User provides a version (e.g. `v1.2.0`) → use it.
- User says `major` / `minor` / `patch` → bump from latest git tag.
- No version given → read latest tag, suggest next minor. Ask to confirm.

#### 2. Pre-flight checks

```bash
git branch --show-current                    # must be "dev"
git fetch origin
git status --porcelain                        # must be clean
git rev-list --left-right --count origin/dev...dev   # must be "0	0" (even with origin/dev)
git tag --sort=-v:refname | head -1           # last release tag
```

**Stop and warn** if not on `dev`, working directory is dirty, or `dev` is not even with `origin/dev` in either direction. If `dev` is only *behind*, offer to fast-forward (`git pull --ff-only`) and continue.

#### 3. Run test suite (fail-fast, recommended but not the mandatory gate)

```bash
dotnet test backend/StarterKit.sln --no-restore -m:1
bun run --cwd frontend test:run
```

**Stop and report** if either suite fails — don't cut a release branch from a known-broken `dev`. (Phase 2's test run is the mandatory gate before shipping; this one just avoids wasted stabilization effort.)

#### 4. Finalize CHANGELOG.md on dev

The `git-commit` skill already adds entries under `## [Unreleased]`. This step promotes that section to the release version.

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

**Do not edit this entry again on the release branch** — stabilization fixes change code, not this section, which keeps the `release/vX.Y.Z → main` merge clean.

**Stop and warn** if `## [Unreleased]` has no entries — there is nothing to release.

Commit and push (already on `dev` per pre-flight):

```bash
git add CHANGELOG.md
git commit -m "docs: update CHANGELOG for vX.Y.Z"
git push origin dev
```

#### 5. Cut the release branch

```bash
git checkout -b release/vX.Y.Z dev
git push origin release/vX.Y.Z
```

#### 6. Summary

Print:

- Release branch name and version
- Reminder: QA stabilizes on `release/vX.Y.Z` from here — bugs found go through `fix/*` branched off `release/vX.Y.Z`, PR'd back into it
- Reminder: run this skill again on `release/vX.Y.Z` (Phase 2) once stabilization is done

**Stop here.** Do not proceed to Phase 2 in the same run — stabilization happens outside this skill, on its own timeline.

---

### Phase 2: Ship (run on `release/vX.Y.Z`)

#### 1. Pre-flight checks

```bash
git branch --show-current                                  # must be "release/vX.Y.Z"
git fetch origin
git status --porcelain                                      # must be clean
git rev-list --left-right --count origin/release/vX.Y.Z...release/vX.Y.Z   # must be "0	0"
```

**Stop and warn** if not on a `release/*` branch, working directory is dirty, or the branch is not even with its remote in either direction.

#### 2. Reconcile with main

A hotfix may have shipped to `main` during this release's stabilization window. Pull it into the release branch before opening the PR, or the merge conflicts (CHANGELOG especially — the hotfix added its own `## [vX.Y.Z]` section).

```bash
git fetch origin
git log --oneline release/vX.Y.Z..origin/main    # commits on main not in the release branch
```

If non-empty:

```bash
git merge origin/main
# resolve conflicts — CHANGELOG: keep BOTH the hotfix section and this release's
# section, hotfix section stays directly above the previous release
git push origin release/vX.Y.Z
```

#### 3. Run test suite — mandatory gate

```bash
dotnet test backend/StarterKit.sln --no-restore -m:1
bun run --cwd frontend test:run
```

**Stop and report** if either suite fails — do not open the release PR. Unlike Phase 1's run, this one cannot be skipped: `fix/*` PRs may have landed on this branch since the cut.

This is the **only** gate before the tag: `.github/workflows/release.yml` triggers on the tag push, so CI runs *after* the release already exists. There is no CI on the `release/* → main` PR — a green local run here is the release's sole pre-tag verification.

#### 4. Create and merge release PR

```bash
gh pr create --base main --head release/vX.Y.Z \
  --title "Release vX.Y.Z" \
  --body "<changelog entries for vX.Y.Z, from CHANGELOG.md>"
gh pr merge <number> --merge --subject "Release vX.Y.Z" --delete-branch
```

#### 5. Tag on main and push

MinVer derives the version from git tags. Always tag `origin/main` after merge — never `dev` or the release branch.

```bash
git fetch origin
git tag vX.Y.Z origin/main
git push origin vX.Y.Z
```

#### 6. Summary

Print:

- Release version
- PR link
- Tag name
- Number of commits included
- **Action for the user:** reconcile `dev` with `main` — `git checkout dev && git pull && git merge origin/main && git push`. Do this before the next `git-commit` or release so `dev` carries the stabilization fixes and the finalized tag history.
- Reminder: delete any merged `fix/*` branches, and drop the local branch with `git branch -d release/vX.Y.Z`

---

## Hotfix Workflow (main → hotfix/vX.Y.Z → main)

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

```bash
git branch --show-current
```

- **Already on `hotfix/vX.Y.Z`** → skip branch creation, continue to step 4.
- **On a different `hotfix/*` branch** → stop and warn: another hotfix is in progress. Confirm the version before continuing.
- **Otherwise** → create it:

  ```bash
  git checkout -b hotfix/vX.Y.Z origin/main
  git push -u origin hotfix/vX.Y.Z
  ```

**Stop here.** Ask the user to apply and commit the code fix on this branch, then confirm when ready to continue. (Step 4's CHANGELOG update is handled by this skill once they confirm — the user only commits the code fix.)

### 4. Update CHANGELOG.md on hotfix branch

`[Unreleased]` on `dev` must NOT be touched from this branch. Instead, insert a new release section directly above the previous release:

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
gh pr merge <number> --merge --subject "Hotfix vX.Y.Z" --delete-branch
```

### 6. Tag on main and push

```bash
git fetch origin
git tag vX.Y.Z origin/main
git push origin vX.Y.Z
```

### 7. Summary

Print:

- Hotfix version
- PR to main (link)
- Tag name
- **Action for the user:** merge `main` back into `dev` (`git checkout dev && git pull && git merge origin/main && git push`) so the fix and its CHANGELOG section reach `dev`. Resolve the CHANGELOG conflict by keeping the hotfix's `## [vX.Y.Z]` section above the previous release; leave `dev`'s `## [Unreleased]` untouched.

---

## Rules

- Never force push.
- Never skip the test suite: Phase 1's run is fail-fast (stop and report on failure, but its absence doesn't itself block a later Phase 2 run); Phase 2's run and the hotfix's are the mandatory gate — a failing suite blocks the release/hotfix (no PR, no merge).
- Never skip the CHANGELOG finalization.
- No version file to bump — MinVer reads the git tag directly.
- Always tag `origin/main` after merge — never `dev`, `release/*`, or the hotfix branch.
- This skill never merges `main` back into `dev`. Reconciling the two is the user's step, done after the release/hotfix ships — the skill only prints the reminder in its final summary. Until the user does it, `dev` is missing the stabilization fixes and hotfixes that landed on `main`.
- Hotfix: never modify `dev`'s `[Unreleased]` section from the hotfix branch — insert the hotfix's own dated CHANGELOG section instead (step 4). It reaches `dev` when the user merges `main` into `dev`.
- If any step fails, stop and report — do not continue.
