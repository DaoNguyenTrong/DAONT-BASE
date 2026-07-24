# Task Completion Checklist

After every non-trivial change, in order:

## Backend changes
1. `dotnet test backend/FEEDBACK-HUB.sln --no-restore -m:1` — must pass
2. If EF entity/config changed: `dotnet ef migrations add <Name> --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API`
3. If API contract changed: update `frontend/src/api/types.ts` to match wire shape (camelCase)

## Frontend changes
1. `bun run --cwd frontend test:run` — must pass
2. `bun run --cwd frontend format` — run prettier
3. If new user-facing text added: update BOTH `src/locales/vi.ts` AND `src/locales/en.ts`
4. If new locale key added: update TypeScript interface in `en.ts` too

## Both sides changed
Run both test suites before committing.

## Before committing
- `npx gitnexus analyze` after committing to re-index
- Log expensive/non-obvious decisions in `.claude/decisions.md`
