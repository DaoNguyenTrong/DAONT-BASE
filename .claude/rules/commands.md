# Build & Migration Commands

Read this file when building, running, or testing either `backend/` or `frontend/`, or working with EF Core migrations.

## Backend (`backend/`)

### Build

```bash
# Serialized — parallel build is broken in this .NET 10 environment
dotnet build backend/FEEDBACK-HUB.sln --no-restore -m:1

# Run API
dotnet run --project backend/src/FeedbackHub.API
```

### EF Core Migrations

```bash
# Apply pending migrations
dotnet ef database update --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API

# Add a new migration
dotnet ef migrations add <MigrationName> --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API
```

In production, migrations are applied automatically on startup via `Database.MigrateAsync`.

### Tests

```bash
dotnet test backend/FEEDBACK-HUB.sln --no-restore -m:1
```

Test projects live under `backend/tests/` (`FeedbackHub.Domain.Tests`, `FeedbackHub.Application.Tests`), mirroring the `backend/src/` layer split. See `docs/unit-testing-plan.md` for the testing strategy and phase roadmap.

## Frontend (`frontend/`)

Package manager is `bun` (see `frontend/bun.lock`).

```bash
# Install deps
bun install --cwd frontend

# Dev server
bun run --cwd frontend dev

# Type-check + production build
bun run --cwd frontend build

# Unit tests (vitest)
bun run --cwd frontend test:run

# E2E tests (playwright — install browsers once with test:e2e:install)
bun run --cwd frontend test:e2e

# Format (prettier — there is no ESLint config in this project; do not add one without confirming with the user)
bun run --cwd frontend format
```

## CI

There is **no CI workflow that runs build/test on PR or push today** — `.github/workflows/` currently only has `release.yml` (used by the `git-release` skill). Nothing blocks a merge on a failing build or test yet; run the commands above locally before asking for review.
