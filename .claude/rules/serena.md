---
paths:
  - "backend/**"
  - "frontend/**"
---

# Serena — Symbol Navigation

When to use Serena vs CodeGraph, and each operation's scope: `AGENTS.md` § Tool Selection. This file records how to call Serena and the detailed limitations behind that table.

## Calling Serena: the REPL interface

Serena ≥ 2.1 runs with `agent_interface: REPL` (pinned in `.serena/project.yml`). It exposes two MCP tools only, `mcp__serena__initial_instructions` and `mcp__serena__serena_repl`; the per-operation tools of Serena 1.x (`mcp__serena__find_symbol`, …) no longer exist.

1. Call `initial_instructions` once per agent. It returns the manual and a session id.
2. Call `serena_repl` with that `session` and Python `code`. Operations live on the object `s`; the value of the last expression is returned (`print` returns nothing). Batch several queries in one call when they are independent.

| Operation                          | REPL call                                                                               |
| ---------------------------------- | --------------------------------------------------------------------------------------- |
| Read a symbol body                 | `s.lsp.find_symbol("Class/Method", relative_path="backend/src/...", include_body=True)` |
| List a file's symbols              | `s.lsp.get_symbols_overview("path/to/file.cs", depth=1)`                                |
| Resolve a usage to its declaration | `s.lsp.find_declaration("path/to/file.ts", r"obj\.(process)\(")`                        |
| Find callers (backend C# only)     | `s.lsp.find_referencing_symbols("Class/Method", "path/to/file.cs")`                     |
| Rename (backend C# only)           | `s.lsp.rename_symbol("Class/Method", "path/to/file.cs", "NewName")`                     |
| Edit by symbol                     | `s.edit.replace_symbol_body` / `insert_after_symbol` / `insert_before_symbol`           |
| Edit a few lines                   | `s.edit.replace_content(path, needle, repl, mode="literal")`                            |
| Diagnostics                        | `s.lsp.get_diagnostics_for_file("path/to/file.cs")`                                     |

Documentation is built in: `s.info("lsp")` for a facade, `s.info("lsp.find_symbol")` for one method. Line numbers in results are 0-based.

- The `shell` facade is excluded: run commands through `Bash`, which passes `scripts/claude-bash-guard.ts`.
- Claude's `Edit` refuses a file it has not `Read`. After reading code through Serena, edit with `s.edit.*`, or `Read` the range before using `Edit`.
- `s.edit.*`, `s.fs.create_text_file` and `s.mem.*` write files. Agents that must not edit code (e.g. `tech-lead`) call only `s.lsp.find_symbol`, `s.lsp.get_symbols_overview` and `s.lsp.find_declaration`.
- Serena's own manual forbids `Read`/`Edit` on code files. This repo does not: `AGENTS.md` § Tool Selection wins (CodeGraph first, then Serena, then `Read`).

## Known limitation: frontend caller-search and rename are unreliable

`find_referencing_symbols` and `rename_symbol` silently under-report frontend callers — verified empirically on Serena 1.x (2026-08-03) and again on 2.1.0 (2026-11-25):

- Component template-tag usage (`<RequiredMark />`, referenced from 6 files): 0/6 found on 1.x. On 2.1.0 a `.vue` file only yields the symbols `template` and `script setup`, so the component cannot even be queried.
- Cross-file function calls, auto-imported (`mapValidationErrors`): 1/3 found on 1.x, and only by chaining two `find_referencing_symbols` calls through the auto-import shim (`src/typings/auto-imports.d.ts`). 2.1.0 finds 0 callers, including the one in a `.ts` file (`use-api-action.ts`).
- Cross-file function calls, **explicitly imported** (`formatDeviceInfo`, real `import` statement): 0/1 on 1.x; 2.1.0 finds the one `.vue` caller. One improved case does not make the tool trustworthy for the others.

Root cause: Serena runs `typescript` and `vue` as separate language-server processes; a `find_referencing_symbols` query rooted in one does not see files owned by the other. `find_declaration` (the reverse direction — usage → declaration) is unaffected and resolves correctly across `.ts`/`.vue` in every case tested, which is why the read-only tools (`find_symbol`, `get_symbols_overview`, `find_declaration`) stay accurate — they never need to search across that process boundary.

**Consequence:** never trust `find_referencing_symbols`/`rename_symbol` for a symbol that might be called from `.vue` — including plain `.ts` functions and utilities, since a `.vue` caller stays invisible even when the query starts from the `.ts` declaration. Use `codegraph_explore`'s blast-radius for impact-check on any frontend symbol instead (verified 100% recall on all three cases above). For a frontend rename, use the blast-radius file list to edit each occurrence manually, then run `bun run --cwd frontend test:run` to confirm nothing was missed.

## Language Scope

`.serena/project.yml` (tracked in git; `cache/`, `memories/` and `project.local.yml` under `.serena/` stay ignored) starts the `language_servers` `csharp`, `vue`, and `typescript`. `find_symbol`/`get_symbols_overview`/`find_declaration` work on `frontend/` (`.ts`/`.vue`). `find_referencing_symbols`/`rename_symbol` do not — see Known Limitation above. Put machine-specific overrides in `.serena/project.local.yml`, never in `project.yml`.

### Gitignored files are invisible to Serena's own search

`ignore_all_files_in_gitignore: true` with `ignored_paths: []` — Serena's file/symbol search skips everything the root `.gitignore` ignores. That is deliberate: it keeps `backend/src/StarterKit.API/appsettings*.json` (real secrets, e.g. the DB connection string) out of every Serena tool, with no hand-maintained list that can drift from `.gitignore`.

Consequence: the generated frontend directories `frontend/src/typings/` (`auto-imports.d.ts`, `components.d.ts`) and `frontend/src/api/generated/` (the orval client) don't show up in `find_symbol`/`get_symbols_overview` results. Read them with `Read`, or query CodeGraph, when you need their content. If you ever flip `ignore_all_files_in_gitignore` to `false` to expose them, you must first add `ignored_paths` entries replicating the root `.gitignore` — at minimum every `appsettings*.json` pattern — or Serena will read the real secrets.
