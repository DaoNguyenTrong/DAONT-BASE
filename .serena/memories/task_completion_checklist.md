# Task Completion Checklist

1. Build the side you touched:
   - Backend: `dotnet build backend/StarterKit.sln --no-restore -m:1`
   - Frontend: `bun run --cwd frontend build`
2. Run tests for the side you touched:
   - Backend: `dotnet test backend/StarterKit.sln --no-restore -m:1`
   - Frontend: `bun run --cwd frontend test:run`
3. If you changed a backend endpoint/DTO, check `frontend/src/api/{resource}-api.ts` and
   `frontend/src/api/types.ts` are still in sync (no codegen wired yet — see `.claude/rules/api-contract.md`).
4. If you added/changed a user-facing message, add both `vi` and `en` entries (backend resx/consts,
   frontend `locales/{vi,en}.ts`) — see `.claude/rules/localization.md`.
5. Run `npx gitnexus analyze` then `gitnexus_detect_changes()` before committing to confirm the
   change's blast radius matches intent.
6. Log only expensive/non-obvious decisions to `.claude/decisions.md` (skip routine ones).
