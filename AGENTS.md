# AGENTS.md

This file provides guidance to AI code when working with this repository.

## Monorepo Layout

Three top-level trees, each with its own stack and rule surface:

| Path        | Stack                                                                     | Relevant rules                                                             |
| ----------- | -------------------------------------------------------------------------- | ----------------------------------------------------------------------------- |
| `backend/`  | .NET 10, Clean Architecture (Domain → Application → Infrastructure → API)  | `architecture.md`, `code-conventions.md`, `authentication.md`, `commands.md`  |
| `frontend/` | Vue 3 + Vite + TypeScript, Pinia, naive-ui, vue-i18n, Tailwind              | `frontend-conventions.md`, `commands.md`                                      |
| `shared/`   | `docs/` (architecture & API docs), `openapi/` (contract exported from `backend`, consumed by `frontend`) | `api-contract.md`                                                             |

`shared/openapi/` is the reason this is a monorepo rather than two separate repos: the backend's OpenAPI export is meant to be the single source of truth the frontend generates its client/types from (see `.claude/decisions.md` — "Monorepo: backend/frontend/shared, overriding..."). Codegen isn't wired up yet — see `api-contract.md` for the current state and what changes when it is.

CodeGraph is indexed repo-wide (backend + frontend); Serena's symbol-navigation LSP is configured for `backend/` (C#) only today — see `serena.md` for how to navigate `frontend/` until that's extended.

## Agent Workflow

### Pre-Task Protocol

Follow this order for every non-trivial task:

1. **Memory** — check `.claude/decisions.md` and project memory for relevant prior decisions
2. **Live state** — run `git status` + `git diff` to understand current working state
3. **Orient (macro)** — CodeGraph (`codegraph_explore`) to understand module boundaries, data flow, and cross-service relationships across both backend and frontend
4. **Orient (micro)** — Serena to locate and read specific symbols once the relevant module is known (backend C# only)
5. **Impact check** — run `codegraph_explore` on every symbol you plan to modify and review the returned blast-radius/callers before editing
6. **Execute** — make changes
7. **Verify** — run the test suite for whichever side you touched (`dotnet test backend/StarterKit.sln --no-restore -m:1` for `backend/`, `bun run --cwd frontend test:run` for `frontend/` — see `commands.md`); it must pass before considering the task done
8. **Log decisions** — record only choices that affect system design (architecture, contracts, security posture) in `.claude/decisions.md`, titled `### YYYY-MM-DD — <decision>`; skip branding/UI/copy calls and routine bug fixes. **Gate: before writing, show the user the proposed entry and get explicit confirmation — never write to `.claude/decisions.md` unprompted.**

### Tool Selection

Two-tier model: **CodeGraph first** when you need orientation; **Serena next** when you know what to look at (backend C# only).

| When you don't yet know where to look…           | Use                                                        |
| ------------------------------------------------ | ----------------------------------------------------------- |
| How do modules / services connect?               | CodeGraph `codegraph_explore` (natural-language query)     |
| What calls what across layers?                   | CodeGraph `codegraph_explore`                               |
| What breaks if I change this?                    | CodeGraph `codegraph_explore` (blast-radius in the response) |
| Rename a symbol safely across the whole codebase | Serena `rename_symbol` (backend C# only — CodeGraph has no rename tool) |

| When you already know which symbol to inspect… | Use                                               |
| ----------------------------------------------- | ------------------------------------------------- |
| Read a class or method body                      | Serena `find_symbol` with `include_body=true`     |
| Find every caller / usage of a symbol            | Serena `find_referencing_symbols`                 |
| Scan a file's public surface                     | Serena `get_symbols_overview`                     |
| Broad keyword / file search                      | `grep` (quick), Explore agent (broad)             |
| Read implementation line-by-line                 | `Read` — only after Serena identified the file    |

Never grep first. Never open a file to orient — orient with CodeGraph, then drill with Serena, then `Read`.

### Context Layers

| Layer                        | Source                                   | When to access                                                          |
| ---------------------------- | ----------------------------------------- | ------------------------------------------------------------------------- |
| **0 — Persistent memory**    | `.claude/decisions.md`, project memory   | Start of task — check for prior decisions relevant to the task          |
| **1 — Always loaded**        | This file                                | Always in context — architecture, constraints, workflow rules           |
| **2a — Architecture map**    | CodeGraph                                | Orientation: module graph, data flow, call chains, cross-service wiring (backend + frontend) |
| **2b — Symbol navigation**   | Serena                                   | Drill-down: symbol bodies, callers, file surfaces                       |
| **3 — File implementation**  | `Read` tool                              | After Layer 2b identified the exact target                              |
| **4 — Live state**           | `git status`, `git diff`, `dotnet build` | Start of task — understand current working state before acting          |

CodeGraph sees the codebase as a **graph of relationships** — it answers "what connects to what."
Serena sees the codebase as a **tree of symbols** — it answers "what does this specific thing contain."
`Read` is the last resort, not the first instinct.

## Critical Invariants

**Entity Pattern** — private constructor + `static Create(XxxParams p)` factory + `Update(XxxParams p)`. Domain validation lives in `Update`. Never construct entities via EF or Mapperly — always use the factory.

## Decision Log

Record only decisions that affect **system design** in `.claude/decisions.md` — architecture, module/layer boundaries, API or data contracts, security posture, or other cross-cutting structural choices that carry a real trade-off, an external constraint, a rejected alternative, or would be costly to re-derive later. Out of scope: visual/branding/UI styling, copy/content wording, and one-off bug fixes — even ones that took real investigation — unless the fix itself changed a structural/contract/security decision. Skip routine decisions — following an existing pattern, a standard CRUD addition, a mechanical rename — even if you had to think about them briefly; those add noise without future value. Write only the **why** — the reasoning and rejected alternative — never "what was done" or "how" (that's git log/diff's job; don't restate file names, method names, or a narrative of the change), and only when the why isn't obvious from reading the code. Title format: `### YYYY-MM-DD — <decision>`. Newest entry at top — prepend new entries directly below the header note, do not append at the bottom. Keep each entry under ~80 words (excluding heading).

**Gate:** never write to `.claude/decisions.md` without asking first. Draft the proposed entry, show it to the user, and wait for explicit confirmation before prepending it.

## Rule Index

All rule files in `.claude/rules/` are auto-loaded. This table documents when each is most relevant.

| File                       | Read when                                                         |
| -------------------------- | ------------------------------------------------------------------ |
| `api-contract.md`          | Changing API endpoints/DTOs that the frontend consumes            |
| `architecture.md`          | Understanding backend layer responsibilities or boundaries        |
| `authentication.md`        | Adding endpoints, headers, or role-based access                   |
| `code-conventions.md`      | Writing C# — naming, EF Core patterns, Mapperly                   |
| `commands.md`              | Building/running/testing the backend or the frontend              |
| `frontend-conventions.md`  | Writing Vue/TypeScript — components, stores, api client patterns  |
| `localization.md`          | Adding or editing user-facing messages (backend and/or frontend)  |
| `serena.md`                | Using Serena symbol navigation tools (backend C# only, for now)   |

<!-- codegraph:start -->
# CodeGraph — Code Intelligence

This project is indexed by CodeGraph (a `.codegraph/` directory at the repo root, covering both `backend/` and `frontend/`). Reach for it BEFORE grep/find or reading files to understand or locate code.

> If the index looks stale, run `codegraph sync` in the terminal first.

## Always Do

- **MCP tool** (when available): `codegraph_explore` — one call returns the relevant symbols' verbatim source plus the call paths between them and a blast-radius summary (callers/dependents) of what depends on them. Name a file or symbol in the query to read its current line-numbered source.
- **Shell** (always works): `codegraph explore "<symbol names or question>"` prints the same output.
- Call it before editing any function, class, or method, and review the blast-radius section before proceeding.
- When exploring unfamiliar code, pass a natural-language question — no separate search step needed.

## Never Do

- NEVER grep or open a file to orient first — query CodeGraph, then drill with Serena (backend C# only), then `Read`.
- NEVER rename symbols with find-and-replace — CodeGraph has no rename tool; use Serena's `rename_symbol` (backend C# only) for LSP-accurate renames.

<!-- codegraph:end -->
