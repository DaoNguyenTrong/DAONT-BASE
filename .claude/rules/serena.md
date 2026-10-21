# Serena — Symbol Navigation

When to use Serena vs CodeGraph, and each tool's scope: `AGENTS.md` § Tool Selection. This file records only the detailed limitations behind that table.

## Known limitation: frontend caller-search and rename are unreliable

`find_referencing_symbols` and `rename_symbol` silently under-report any caller that lives in a `.vue` file — verified empirically (2026-08-03):

- Component template-tag usage (`<RequiredMark />`, referenced from 6 files): 0/6 found.
- Cross-file function calls, auto-imported (`mapValidationErrors`): 1/3 found, and only by manually chaining two `find_referencing_symbols` calls through the auto-import shim file (`src/typings/auto-imports.d.ts`).
- Cross-file function calls, **explicitly imported** (`formatDeviceInfo`, real `import` statement, no auto-import involved): 0/1 found — proves the gap is not caused by auto-import.

Root cause: Serena runs `typescript` and `vue` as separate language-server processes; a `find_referencing_symbols` query rooted in one does not see files owned by the other. `find_declaration` (the reverse direction — usage → declaration) is unaffected and resolves correctly across `.ts`/`.vue` in every case tested, which is why the read-only tools (`find_symbol`, `get_symbols_overview`, `find_declaration`) stay accurate — they never need to search across that process boundary.

**Consequence:** never trust `find_referencing_symbols`/`rename_symbol` for a symbol that might be called from `.vue` — including plain `.ts` functions and utilities, since a `.vue` caller stays invisible even when the query starts from the `.ts` declaration. Use `codegraph_explore`'s blast-radius for impact-check on any frontend symbol instead (verified 100% recall on all three cases above). For a frontend rename, use the blast-radius file list to edit each occurrence manually, then run `bun run --cwd frontend test:run` to confirm nothing was missed.

## Language Scope

`.serena/project.yml` (tracked in git; `cache/`, `memories/` and `project.local.yml` under `.serena/` stay ignored) starts language servers for `csharp`, `vue`, and `typescript`. `find_symbol`/`get_symbols_overview`/`find_declaration` work on `frontend/` (`.ts`/`.vue`). `find_referencing_symbols`/`rename_symbol` do not — see Known Limitation above. Put machine-specific overrides in `.serena/project.local.yml`, never in `project.yml`.

### Gitignored files are invisible to Serena's own search

`ignore_all_files_in_gitignore: true` with `ignored_paths: []` — Serena's file/symbol search skips everything the root `.gitignore` ignores. That is deliberate: it keeps `backend/src/StarterKit.API/appsettings*.json` (real secrets, e.g. the DB connection string) out of every Serena tool, with no hand-maintained list that can drift from `.gitignore`.

Consequence: the generated frontend directories `frontend/src/typings/` (`auto-imports.d.ts`, `components.d.ts`) and `frontend/src/api/generated/` (the orval client) don't show up in `find_symbol`/`get_symbols_overview` results. Read them with `Read`, or query CodeGraph, when you need their content. If you ever flip `ignore_all_files_in_gitignore` to `false` to expose them, you must first add `ignored_paths` entries replicating the root `.gitignore` — at minimum every `appsettings*.json` pattern — or Serena will read the real secrets.
