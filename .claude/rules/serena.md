# Serena — Symbol Navigation

Call `serena__initial_instructions` at the start of any coding task to load Serena's working instructions.

**When to reach for Serena:** after CodeGraph has told you *which* module or class is involved. Serena is the drill; CodeGraph is the map. If you don't yet know where to look, orient with CodeGraph first.

| Tool                        | When to use                                                                                                    | Scope |
|------------------------------|------------------------------------------------------------------------------------------------------------------|-------|
| `find_symbol`                | Locate a class, method, or property by name; read its body                                                       | backend + frontend |
| `find_declaration`           | From a usage site, jump to its declaration                                                                       | backend + frontend |
| `get_symbols_overview`       | Scan a file's public surface without reading its full body                                                       | backend + frontend |
| `find_referencing_symbols`   | Find every caller or usage of a symbol                                                                            | **backend C# only** — see Known Limitation below |
| `rename_symbol`              | Rename a symbol with LSP accuracy — the only graph-aware rename tool available (CodeGraph has no rename tool)     | **backend C# only** — see Known Limitation below |

Serena operates at the LSP/symbol level — precise, file-scoped. For cross-module call graphs and service wiring, use CodeGraph instead.

## Known limitation: frontend caller-search and rename are unreliable

`find_referencing_symbols` and `rename_symbol` silently under-report any caller that lives in a `.vue` file — verified empirically (2026-08-03):

- Component template-tag usage (`<RequiredMark />`, referenced from 6 files): 0/6 found.
- Cross-file function calls, auto-imported (`mapValidationErrors`): 1/3 found, and only by manually chaining two `find_referencing_symbols` calls through the auto-import shim file (`src/typings/auto-imports.d.ts`).
- Cross-file function calls, **explicitly imported** (`formatDeviceInfo`, real `import` statement, no auto-import involved): 0/1 found — proves the gap is not caused by auto-import.

Root cause: Serena runs `typescript` and `vue` as separate language-server processes; a `find_referencing_symbols` query rooted in one does not see files owned by the other. `find_declaration` (the reverse direction — usage → declaration) is unaffected and resolves correctly across `.ts`/`.vue` in every case tested, which is why the read-only tools above stay accurate — they never need to search across that process boundary.

**Consequence:** never trust `find_referencing_symbols`/`rename_symbol` for a symbol that might be called from `.vue` — including plain `.ts` functions and utilities, since a `.vue` caller stays invisible even when the query starts from the `.ts` declaration. Use `codegraph_explore`'s blast-radius for impact-check on any frontend symbol instead (verified 100% recall on all three cases above). For a frontend rename, use the blast-radius file list to edit each occurrence manually, then run `bun run --cwd frontend test:run` to confirm nothing was missed.

## Language Scope

`.serena/project.yml` starts language servers for `csharp`, `typescript`, and `vue`. `find_symbol`/`get_symbols_overview`/`find_declaration` work correctly on `frontend/` (`.ts`/`.vue`). `find_referencing_symbols`/`rename_symbol` do not — see Known Limitation above.

### Gitignored files Serena must still read

`ignore_all_files_in_gitignore` is set to `false` in `project.yml`, with `ignored_paths` manually replicating the repo's root `.gitignore` — required because two gitignored, generated directories must stay visible to the TS/Vue language servers: `frontend/src/typings/` (`auto-imports.d.ts`, `components.d.ts` — the actual bridge files auto-import relies on) and `frontend/src/api/generated/` (the orval-generated OpenAPI client). Whenever the root `.gitignore` changes, `ignored_paths` in `project.yml` must be updated by hand to match — it is no longer derived automatically via `ignore_all_files_in_gitignore`. The `appsettings*.json` entries in that list are security-load-bearing (contain real secrets, e.g. the DB connection string) — never remove them when syncing.
