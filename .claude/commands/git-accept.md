---
description: Merge the current branch's docs-only PR into dev when the gate passes (required CI green, agent review without blocking findings, docs-only scope)
---

Merge a docs-only pull request into `dev` through a gate. This command is the only merge path that does not need the user's explicit order; any other merge (code, hotfix, release) needs one (`AGENTS.md` § Commits). The gate runs on the client, so it is not a hard control: it only enforces the process.

## Steps

### 1. Find the PR

```bash
gh pr view --json number,state,baseRefName,headRefName,url,title
```

Stop unless `state` is `OPEN`, `baseRefName` is `dev`, and `headRefName` starts with `feature/`, `fix/` or `chore/`. Report which condition failed.

### 2. Scope gate

```bash
gh pr view <number> --json files --jq '.files[].path'
```

Check each path in turn, in this order:

1. Under `.claude/` or `.github/` → fail, even when it ends in `.md` (rules and agent prompts change the process, so they are reviewed by hand).
2. Under `docs/`, or ends in `.md` → pass.
3. Anything else → fail.

Any failing path → stop and report the first offending file. Code, workflows, rules, settings and anything touching auth are merged by hand.

### 3. CI gate

```bash
gh pr view <number> --json statusCheckRollup --jq '.statusCheckRollup[] | "\(.name)\t\(.conclusion // .status)"'
```

- `changes` and `test / comments` must be `SUCCESS` (`comments` never skips), and `test / backend`, `test / frontend` and `test / guide` must each be `SUCCESS` or `SKIPPED`. CI skips the parts a change does not touch and counts a skip as passing (see the header of `.github/workflows/ci.yml`), so a docs-only PR usually shows them skipped.
- Any of those checks missing, or any check `FAILURE`, `CANCELLED`, `PENDING`, `QUEUED` or `IN_PROGRESS` → stop. Do not wait in a loop; report which check is not finished.

### 4. Review gate

Dispatch the `code-reviewer` subagent (Agent tool, `subagent_type: code-reviewer`) and give it the PR number, so it reviews `gh pr diff <number>`. It checks the docs against the rules they describe (for example, a doc that names a file or command that no longer exists) and labels each finding `blocking` or `non-blocking`.

Post its output as a PR comment so the decision is on record:

```bash
gh pr comment <number> --body-file <file>
```

Write the body to the scratchpad directory, not `/tmp`. Any finding labelled `blocking` → stop and report it. Non-blocking findings do not stop the merge.

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
