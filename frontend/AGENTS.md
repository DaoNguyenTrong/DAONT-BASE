# Frontend

Before editing anything under `frontend/`, read these files in `.claude/rules/`:

- `frontend-conventions.md`
- `commands.md`
- `comments.md`
- `testing.md`
- `api-contract.md` when the change touches the generated client or a request the backend owns
- `localization.md` when the change touches UI copy

Do not use Serena `find_referencing_symbols` or `rename_symbol` on frontend symbols. Use CodeGraph. Details: `.claude/rules/serena.md`. The repository root `AGENTS.md` still applies.
