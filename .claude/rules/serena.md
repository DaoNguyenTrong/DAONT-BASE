# Serena — Symbol Navigation

Call `serena__initial_instructions` at the start of any coding task to load Serena's working instructions.

**When to reach for Serena:** after GitNexus has told you *which* module or class is involved. Serena is the drill; GitNexus is the map. If you don't yet know where to look, orient with GitNexus first.

| Tool                       | When to use                                                                                          |
|----------------------------|------------------------------------------------------------------------------------------------------|
| `find_symbol`              | Locate a class, method, or property by name; read its body                                           |
| `find_referencing_symbols` | Find every caller or usage of a symbol                                                               |
| `get_symbols_overview`     | Scan a file's public surface without reading its full body                                           |
| `rename_symbol`            | Rename a symbol with LSP accuracy (use GitNexus `gitnexus_rename` for cross-file graph-aware rename) |

Serena operates at the LSP/symbol level — precise, file-scoped. For cross-module call graphs and service wiring, use GitNexus instead.

## Language Scope

`.serena/project.yml` only starts a language server for `csharp` — Serena's symbol tools (`find_symbol`, `find_referencing_symbols`, `get_symbols_overview`, `rename_symbol`) work for `backend/` only. They will not resolve anything in `frontend/` (Vue/TypeScript).

For `frontend/` work, until a `typescript`/`vue` language server is added to `project.yml`:

- Use `grep` or the Explore agent for locating components/composables/stores.
- Use `Read` directly once the file is found — there is no LSP drill-down step to reach for.
- Do not assume Serena's language list is current without checking `.serena/project.yml` first — adding `typescript`/`vue` there requires verifying the corresponding language server can actually start in this environment before relying on it.
