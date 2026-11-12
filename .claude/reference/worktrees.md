# Worktrees

A git worktree (including an agent's `isolation: "worktree"`) has only tracked files: no `backend/src/StarterKit.API/appsettings*.json`, no `.env*`, no `frontend/node_modules`, `frontend/src/api/generated/` or `frontend/src/typings/` (`bun install --cwd frontend` regenerates `api/generated/`; `typings/` comes from a Vite run and is complete only after `bun run --cwd frontend build-only` — `type-check` fails without it). Copy the gitignored config in before building or testing, and remove the worktree (`git worktree remove <path>`) when done.

`isolation: "worktree"` branches from `origin`'s default branch, not the local `HEAD` — an agent there does not see unpushed local commits, so hand it the branch or the diff explicitly.
