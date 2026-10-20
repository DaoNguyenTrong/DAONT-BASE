---
description: Merge the current branch's docs-only PR into dev when the gate passes (required CI green, agent review without blocking findings, docs-only scope)
---

Merge a docs-only pull request into `dev` through a gate. This command is the only sanctioned merge path for work that is not a hotfix or release. The gate runs on the client, so it is not a hard control: it only enforces the process.

Run only on a `feature/*`, `fix/*` or `chore/*` branch that has an open PR into `dev`.

## Steps

### 1. Find the PR

```bash
gh pr view --json number,state,baseRefName,headRefName,url,title
```

Stop unless `state` is `OPEN` and `baseRefName` is `dev`. Report which condition failed.

### 2. Scope gate

```bash
gh pr view <number> --json files --jq '.files[].path'
```

Every path must match `*.md` or `docs/**`, and must not sit under `.claude/` or `.github/` even when it ends in `.md` (rules and agent prompts change the process, so they are reviewed by hand). Any other path → stop and report the first offending file. Code, workflows, rules, settings and anything touching auth are merged by hand.

Check each path in turn: `.claude/*` and `.github/*` fail; `docs/*` and `*.md` pass; anything else fails. Any failing path → stop.

### 3. CI gate

```bash
gh pr view <number> --json statusCheckRollup --jq '.statusCheckRollup[] | "\(.name)\t\(.conclusion // .status)"'
```

- `test / backend` and `test / frontend` must be `SUCCESS`. A `SKIPPED` for either is reported and allowed only when the `changes` job shows no backend or frontend change, which step 2 already guarantees for docs-only PRs.
- Any `FAILURE`, `PENDING`, `QUEUED` or `IN_PROGRESS` → stop. Do not wait in a loop; report which check is not finished.

### 4. Review gate

Run the `code-reviewer` agent on the PR diff (`git diff origin/dev...HEAD`). It checks the docs against the rules they describe (for example, a doc that names a file or command that no longer exists).

Post its output as a PR comment so the decision is on record:

```bash
gh pr comment <number> --body-file <file>
```

Write the body to the scratchpad directory, not `/tmp`. Any finding the reviewer marks as blocking → stop and report it. Non-blocking findings do not stop the merge.

### 5. Merge

Only when steps 1–4 pass:

```bash
gh pr merge <number> --merge
```

Use `--merge` to match the repo's existing merge commits. Never pass `--admin`, `--auto` or `--delete-branch` here.

### 6. Finish

Print the merge commit and the PR URL. Remind the user to run `/git-sync` on any other branch that was based on `dev`, and to delete the local branch once it is no longer needed.

## Rules

- Never merge a PR that touches a path outside `*.md` and `docs/**`
- Never bypass a failed gate with `--admin` or by editing the checks
- Never force push
- If any step fails, stop and report which gate failed and why
