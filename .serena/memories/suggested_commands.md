# Suggested Commands

## Backend (`backend/`)

```bash
# Build (serialized — parallel broken in this .NET 10 env)
dotnet build backend/FEEDBACK-HUB.sln --no-restore -m:1

# Run API
dotnet run --project backend/src/FeedbackHub.API

# Tests
dotnet test backend/FEEDBACK-HUB.sln --no-restore -m:1

# EF Core migrations
dotnet ef migrations add <Name> --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API
dotnet ef database update --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API
```

## Frontend (`frontend/`) — package manager is `bun`

```bash
bun install --cwd frontend

bun run --cwd frontend dev          # dev server
bun run --cwd frontend build        # type-check + production build
bun run --cwd frontend test:run     # unit tests (vitest)
bun run --cwd frontend test:e2e     # e2e tests (playwright)
bun run --cwd frontend format       # prettier (no ESLint in this project)
```

## Code Intelligence

```bash
npx gitnexus analyze   # re-index GitNexus after significant changes
```
