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

GitNexus and Serena are indexed/configured for `backend/` (C#) only today — see `serena.md` for how to navigate `frontend/` until that's extended.

## Agent Workflow

### Pre-Task Protocol

Follow this order for every non-trivial task:

1. **Memory** — check `.claude/decisions.md` and project memory for relevant prior decisions
2. **Live state** — run `git status` + `git diff` to understand current working state
3. **Orient (macro)** — GitNexus to understand module boundaries, data flow, and cross-service relationships
4. **Orient (micro)** — Serena to locate and read specific symbols once the relevant module is known
5. **Impact check** — run `gitnexus_impact` on every symbol you plan to modify
6. **Execute** — make changes
7. **Verify** — run `gitnexus_detect_changes` to confirm scope matches intent, then run the test suite for whichever side you touched (`dotnet test backend/StarterKit.sln --no-restore -m:1` for `backend/`, `bun run --cwd frontend test:run` for `frontend/` — see `commands.md`); it must pass before considering the task done
8. **Log decisions** — record only expensive, non-obvious choices in `.claude/decisions.md`; skip routine ones

### Tool Selection

Two-tier model: **GitNexus first** when you need orientation; **Serena next** when you know what to look at.

| When you don't yet know where to look…          | Use                                               |
| ----------------------------------------------- | ------------------------------------------------- |
| How do modules / services connect?               | GitNexus `gitnexus_query`, `gitnexus_context`     |
| What calls what across layers?                   | GitNexus `gitnexus_query`                         |
| What breaks if I change this?                    | GitNexus `gitnexus_impact`                        |
| Rename a symbol safely across the whole codebase | GitNexus `gitnexus_rename`                        |

| When you already know which symbol to inspect… | Use                                               |
| ----------------------------------------------- | ------------------------------------------------- |
| Read a class or method body                      | Serena `find_symbol` with `include_body=true`     |
| Find every caller / usage of a symbol            | Serena `find_referencing_symbols`                 |
| Scan a file's public surface                     | Serena `get_symbols_overview`                     |
| Broad keyword / file search                      | `grep` (quick), Explore agent (broad)             |
| Read implementation line-by-line                 | `Read` — only after Serena identified the file    |

Never grep first. Never open a file to orient — orient with GitNexus, then drill with Serena, then `Read`.

### Context Layers

| Layer                        | Source                                   | When to access                                                          |
| ---------------------------- | ----------------------------------------- | ------------------------------------------------------------------------- |
| **0 — Persistent memory**    | `.claude/decisions.md`, project memory   | Start of task — check for prior decisions relevant to the task          |
| **1 — Always loaded**        | This file                                | Always in context — architecture, constraints, workflow rules           |
| **2a — Architecture map**    | GitNexus                                 | Orientation: module graph, data flow, call chains, cross-service wiring |
| **2b — Symbol navigation**   | Serena                                   | Drill-down: symbol bodies, callers, file surfaces                       |
| **3 — File implementation**  | `Read` tool                              | After Layer 2b identified the exact target                              |
| **4 — Live state**           | `git status`, `git diff`, `dotnet build` | Start of task — understand current working state before acting          |

GitNexus sees the codebase as a **graph of relationships** — it answers "what connects to what."
Serena sees the codebase as a **tree of symbols** — it answers "what does this specific thing contain."
`Read` is the last resort, not the first instinct.

## Critical Invariants

**Entity Pattern** — private constructor + `static Create(XxxParams p)` factory + `Update(XxxParams p)`. Domain validation lives in `Update`. Never construct entities via EF or Mapperly — always use the factory.

## Decision Log

Record only **expensive** decisions in `.claude/decisions.md` — ones that carry a real trade-off, an external constraint, a rejected alternative, or would be costly to re-derive later (irreversible choices, cross-cutting design calls, anything a future session could easily get wrong without the reasoning). Skip routine decisions — following an existing pattern, a standard CRUD addition, a straightforward bug fix, a mechanical rename — even if you had to think about them briefly; those add noise without future value. Write only the **why** — the reasoning and rejected alternative — never "what was done" or "how" (that's git log/diff's job; don't restate file names, method names, or a narrative of the change), and only when the why isn't obvious from reading the code. Newest entry at top — prepend new entries directly below the header note, do not append at the bottom. Keep each entry under ~80 words (excluding heading).

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

<!-- gitnexus:start -->
# GitNexus — Code Intelligence

This project is indexed by GitNexus as **DAONT-BASE** (3571 symbols, 7972 relationships, 198 execution flows). Use the GitNexus MCP tools to understand code, assess impact, and navigate safely.

> If any GitNexus tool warns the index is stale, run `npx gitnexus analyze` in terminal first.

## Always Do

- **MUST run impact analysis before editing any symbol.** Before modifying a function, class, or method, run `gitnexus_impact({target: "symbolName", direction: "upstream"})` and report the blast radius (direct callers, affected processes, risk level) to the user.
- **MUST run `gitnexus_detect_changes()` before committing** to verify your changes only affect expected symbols and execution flows.
- **MUST warn the user** if impact analysis returns HIGH or CRITICAL risk before proceeding with edits.
- When exploring unfamiliar code, use `gitnexus_query({query: "concept"})` to find execution flows instead of grepping. It returns process-grouped results ranked by relevance.
- When you need full context on a specific symbol — callers, callees, which execution flows it participates in — use `gitnexus_context({name: "symbolName"})`.

## Never Do

- NEVER edit a function, class, or method without first running `gitnexus_impact` on it.
- NEVER ignore HIGH or CRITICAL risk warnings from impact analysis.
- NEVER rename symbols with find-and-replace — use `gitnexus_rename` which understands the call graph.
- NEVER commit changes without running `gitnexus_detect_changes()` to check affected scope.

## Resources

| Resource | Use for |
|----------|---------|
| `gitnexus://repo/DAONT-BASE/context` | Codebase overview, check index freshness |
| `gitnexus://repo/DAONT-BASE/clusters` | All functional areas |
| `gitnexus://repo/DAONT-BASE/processes` | All execution flows |
| `gitnexus://repo/DAONT-BASE/process/{name}` | Step-by-step execution trace |

## CLI

| Task | Read this skill file |
|------|---------------------|
| Understand architecture / "How does X work?" | `.claude/skills/gitnexus/gitnexus-exploring/SKILL.md` |
| Blast radius / "What breaks if I change X?" | `.claude/skills/gitnexus/gitnexus-impact-analysis/SKILL.md` |
| Trace bugs / "Why is X failing?" | `.claude/skills/gitnexus/gitnexus-debugging/SKILL.md` |
| Rename / extract / split / refactor | `.claude/skills/gitnexus/gitnexus-refactoring/SKILL.md` |
| Tools, resources, schema reference | `.claude/skills/gitnexus/gitnexus-guide/SKILL.md` |
| Index, status, clean, wiki CLI commands | `.claude/skills/gitnexus/gitnexus-cli/SKILL.md` |

<!-- gitnexus:end -->
