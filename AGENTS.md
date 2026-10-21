# AGENTS.md

This file provides guidance to Claude Code when working with this repository.

## Monorepo Layout

Three top-level trees, each with its own stack and rule surface:

| Path        | Stack                                                                     | Relevant rules                                                                              |
| ----------- | ------------------------------------------------------------------------- | ------------------------------------------------------------------------------------------- |
| `backend/`  | .NET 10, Clean Architecture (Domain → Application → Infrastructure → API) | `architecture.md`, `code-conventions.md`, `authentication.md`, `commands.md`, `comments.md` |
| `frontend/` | Vue 3 + Vite + TypeScript, Pinia, naive-ui, vue-i18n, Tailwind            | `frontend-conventions.md`, `commands.md`, `comments.md`                                     |
| `shared/`   | `openapi/` (contract exported from `backend`, consumed by `frontend`)     | `api-contract.md`                                                                           |

`shared/openapi/` is the reason this is a monorepo rather than two separate repos: the backend's OpenAPI export is meant to be the single source of truth the frontend generates its client/types from (see `.claude/decisions.md` — "Monorepo: backend/frontend/shared, overriding..."). Codegen is wired up: `orval` regenerates `frontend/src/api/generated/**` (gitignored) from the committed `openapi.json` on every `bun install` — see `api-contract.md` for the regenerate workflow.

CodeGraph is indexed repo-wide (backend + frontend) and is the tool for cross-file impact-check on both sides. Serena's symbol-navigation LSP covers `backend/` (C#), plus `frontend/` TypeScript and Vue for read-only symbol lookup — but its caller-search/rename tools (`find_referencing_symbols`, `rename_symbol`) are reliable for `backend/` (C#) only; see `serena.md` for why.

## Common Commands

```bash
backend/scripts/test.sh                          # backend tests
bun run --cwd frontend test:run                  # frontend unit tests
bun run --cwd frontend build-only                # writes src/typings/ — required before type-check
bun run --cwd frontend type-check
scripts/test-gate.sh                             # full local gate before push (no coverage)
```

Full list (build, dev, codegen, migrations, OpenAPI export): `.claude/rules/commands.md`.
CI workflows and git hooks (Lefthook): `.claude/reference/ci-and-git-hooks.md`.

## Agent Workflow

### Pre-Task Protocol

Follow this order for every non-trivial task:

1. **Memory** — check `.claude/decisions.md` and project memory for relevant prior decisions
2. **Live state** — run `git status` + `git diff` to understand current working state
3. **Orient (macro)** — CodeGraph (`codegraph_explore`) to understand module boundaries, data flow, and cross-service relationships across both backend and frontend
4. **Orient (micro)** — for backend C#, use Serena (`find_symbol`/`get_symbols_overview`) to drill into a specific symbol once CodeGraph has located the module. For frontend, CodeGraph's step-3 response already includes verbatim source — skip a separate Serena call unless you need `find_declaration` to confirm what a specific template-tag usage resolves to.
5. **Impact check** — before editing, review every caller of the symbol you plan to modify. For backend C# symbols, prefer Serena's `find_referencing_symbols` (exact call sites with line-level snippets); fall back to `codegraph_explore` only when the symbol has dynamic dispatch across multiple implementations (interface/base-class methods). For any frontend symbol (`.ts` or `.vue`), use `codegraph_explore`'s blast-radius — never Serena's `find_referencing_symbols`/`rename_symbol`, which silently miss any caller inside a `.vue` file regardless of import style (verified, not a config gap).
5b. **Feature map** — for a change to a user-facing flow, read `docs/feature-map.md` for the affected feature: entry path, test accounts, selectors, how to verify. If the feature has no entry, note it and propose one.
5c. **Reproduce first (bug fixes)** — reproduce the failure before editing: a failing regression test for backend/frontend logic (`testing.md`), or the failing request/UI step through the running stack (`scripts/dev-up.sh`, then `scripts/verify/`). Keep the evidence under `verify-output/`.
6. **Execute** — make changes in small, atomic steps; one concern per PR. Prefer repo generators/templates over hand-writing structure.
7. **Verify** — every gate below must pass; never disable or suppress a rule to get there:
   - a. Unit/integration tests for the side touched (`backend/scripts/test.sh` for `backend/`, `bun run --cwd frontend test:run` for `frontend/` — see `commands.md`)
   - b. Architecture tests and static checks (`ArchitectureTests.cs`, `bun run --cwd frontend type-check`, and `bun run --cwd frontend lint:boundaries` for frontend import boundaries)
   - c. For a change to a user-facing flow: start the stack with `scripts/dev-up.sh`, run the matching `scripts/verify/*.sh` and `bun run --cwd frontend test:e2e`, then `scripts/dev-down.sh`. Attach the output paths under `verify-output/` to the report.
   - d. For a bug fix: show the failure from 5c no longer reproduces
8. **Log decisions** — record only choices that affect system design (architecture, contracts, security posture) in `.claude/decisions.md`, titled `### YYYY-MM-DD — <decision>`, written in English; skip branding/UI/copy calls and routine bug fixes. **Gate: before writing, show the user the proposed entry and get explicit confirmation — never write to `.claude/decisions.md` unprompted.**

**Recurring feedback:** if the same correction comes up twice (from a reviewer or from your own earlier work), stop and turn it into a lint rule, architecture test, or generator instead of adding another prose rule.

### Worktrees

A git worktree (including an agent's `isolation: "worktree"`) has only tracked files: no `backend/src/StarterKit.API/appsettings*.json`, no `.env*`, no `frontend/node_modules`, `frontend/src/api/generated/` or `frontend/src/typings/` (`bun install --cwd frontend` regenerates `api/generated/`; `typings/` comes from a Vite run and is complete only after `bun run --cwd frontend build-only` — `type-check` fails without it). Copy the gitignored config in before building or testing, and remove the worktree (`git worktree remove <path>`) when done. `isolation: "worktree"` branches from `origin`'s default branch, not the local `HEAD` — an agent there does not see unpushed local commits, so hand it the branch or the diff explicitly.

### Commits

- A question is not an order to commit — "is this ready?" or "what changed?" gets an answer, not a commit.
- Run `git branch --show-current` before every commit; never commit on `main` (the Lefthook branch guard blocks it) or on a branch you didn't expect.
- Stage only the files the task changed, by explicit path — never `git add -A`/`git add .`/`git commit -a`; leave changes that predate the task unstaged. Subagents working in parallel each commit their own paths, so one never sweeps up another's half-finished files.
- A commit rejected by Lefthook (commitlint, prettier, secretlint) means fix the message or re-stage — not `--no-verify`.
- Push or open a PR only on the user's explicit order.
- Merge only through `/git-accept`, and only for a docs-only PR that passes its gate (see `.claude/commands/git-accept.md`). Any other merge needs the user's explicit order.

### Tool Selection

Two-tier model: **CodeGraph first** when you need orientation; **Serena next** when you know what to look at. Serena's reliable scope differs per capability — see the Scope column below, not just "is the language server configured."

| When you don't yet know where to look… | Use                                                                                                      |
| -------------------------------------- | -------------------------------------------------------------------------------------------------------- |
| How do modules / services connect?     | CodeGraph `codegraph_explore` (natural-language query)                                                   |
| What calls what across layers?         | CodeGraph `codegraph_explore`                                                                            |
| What breaks if I change this?          | CodeGraph `codegraph_explore` (blast-radius in the response) — the default for frontend impact-check too |

| When you already know which symbol to inspect…   | Use                                                      | Scope                                                                                                                                                                             |
| ------------------------------------------------ | -------------------------------------------------------- | --------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| Read a class or method body                      | Serena `find_symbol` with `include_body=true`            | backend + frontend                                                                                                                                                                |
| Confirm what a specific usage resolves to        | Serena `find_declaration`                                | backend + frontend                                                                                                                                                                |
| Scan a file's public surface                     | Serena `get_symbols_overview`                            | backend + frontend                                                                                                                                                                |
| Find every caller / usage of a symbol            | Serena `find_referencing_symbols`                        | **backend C# only** — for frontend use CodeGraph's blast-radius (verified 0% recall on `.vue` callers otherwise, even with explicit imports)                                      |
| Rename a symbol safely across the whole codebase | Serena `rename_symbol`                                   | **backend C# only** — CodeGraph has no rename tool, and no frontend rename tool here is reliable; rename manually against CodeGraph's blast-radius file list, then run `test:run` |
| Broad keyword / file search                      | `grep` (quick), Explore agent (broad)                    | —                                                                                                                                                                                 |
| Read implementation line-by-line                 | `Read` — only after CodeGraph/Serena identified the file | —                                                                                                                                                                                 |

Never grep first. Never open a file to orient — orient with CodeGraph, then drill with Serena (backend, or `find_declaration` anywhere), then `Read`.

### Context Layers

| Layer                       | Source                                   | When to access                                                                               |
| --------------------------- | ---------------------------------------- | -------------------------------------------------------------------------------------------- |
| **0 — Persistent memory**   | `.claude/decisions.md`, project memory   | Start of task — check for prior decisions relevant to the task                               |
| **1 — Always loaded**       | This file                                | Always in context — architecture, constraints, workflow rules                                |
| **2a — Architecture map**   | CodeGraph                                | Orientation: module graph, data flow, call chains, cross-service wiring (backend + frontend) |
| **2b — Symbol navigation**  | Serena                                   | Drill-down: symbol bodies, callers, file surfaces                                            |
| **3 — File implementation** | `Read` tool                              | After Layer 2b identified the exact target                                                   |
| **4 — Live state**          | `git status`, `git diff`, `dotnet build` | Start of task — understand current working state before acting                               |

CodeGraph sees the codebase as a **graph of relationships** — it answers "what connects to what."
Serena sees the codebase as a **tree of symbols** — it answers "what does this specific thing contain."
`Read` is the last resort, not the first instinct.

### Superpowers Integration

If the superpowers plugin is installed, its skills yield to this file. Where a skill's default disagrees, these win:

- **Where it fits** — brainstorming / systematic-debugging / writing-plans run after Pre-Task steps 1–4: their "explore the context" _is_ memory → live state → CodeGraph → Serena.
- **Specs and plans** go in `docs/superpowers/{specs,plans}/` (the plugin's default). Root `plans/` holds the earlier numbered plans — don't add new ones there. A spec or plan is **not committed** unless the user says so, whatever the skill's own "commit the design doc" step says.
- **Requirement gaps stop the run** — subagent-driven development's "rulings, not stalls" covers technical ambiguity only. A missing or conflicting business rule or acceptance criterion means stop and ask; never write a ruling that invents business behavior.
- **Branch & worktree** — work in the current checkout on one `feature/*` branch cut from `dev`. Don't create a worktree for a skill run: the gitignored config and generated client are missing there (see Worktrees). Subagents that build the backend run one at a time (`-m:1`, shared `obj/`).
- **Commits** — implementers may commit per task on `feature/*` (local only, never on `dev`/`main`) so per-task review ranges work, following Commits above.
- **TDD** — follow `superpowers:test-driven-development` within `testing.md` (which layer, which conventions; a bug fix starts from a failing regression test). "Delete the code and start over" applies only to code written in the current task, never to changes already on the branch.
- **Tests** — baseline and verification use `backend/scripts/test.sh` and `bun run --cwd frontend test:run`.
- **Finishing** — base is `dev` (`main` only for a hotfix); never merge locally or delete the branch. Without an explicit order to push or open a PR, leave the branch as it is.
- **Gated side effects** — push/PR and `.claude/decisions.md` (confirmation gate) still need the user, even mid-plan.

## Critical Invariants

**Entity Pattern** — entities are built only through their `static Create(XxxParams p)` factory (validation in `Update`), never via EF, Mapperly or by hand. Details: `code-conventions.md` § Entity Pattern; enforced by `ArchitectureTests.cs`.

## Decision Log

Record only decisions that affect **system design** in `.claude/decisions.md` — architecture, module/layer boundaries, API or data contracts, security posture, or other cross-cutting structural choices that carry a real trade-off, an external constraint, a rejected alternative, or would be costly to re-derive later. Out of scope: visual/branding/UI styling, copy/content wording, and one-off bug fixes — even ones that took real investigation — unless the fix itself changed a structural/contract/security decision. Skip routine decisions — following an existing pattern, a standard CRUD addition, a mechanical rename — even if you had to think about them briefly; those add noise without future value. Write only the **why** — the reasoning and rejected alternative — never "what was done" or "how" (that's git log/diff's job; don't restate file names, method names, or a narrative of the change), and only when the why isn't obvious from reading the code. Title format: `### YYYY-MM-DD — <decision>`. Write every entry (title and body) in English, whatever language the conversation is in. Newest entry at top — prepend new entries directly below the header note, do not append at the bottom. Keep each entry under ~80 words (excluding heading).

**Gate:** never write to `.claude/decisions.md` without asking first. Draft the proposed entry, show it to the user, and wait for explicit confirmation before prepending it.
