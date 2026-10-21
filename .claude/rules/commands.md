---
paths:
  - "backend/**"
  - "frontend/**"
---

# Build & Migration Commands

Read this file when building, running, or testing either `backend/` or `frontend/`, or working with EF Core migrations.

## Backend (`backend/`)

### Build

```bash
# Serialized — parallel build is broken in this .NET 10 environment
dotnet build backend/StarterKit.sln --no-restore -m:1

# Run API
dotnet run --project backend/src/StarterKit.API
```

### EF Core Migrations

```bash
# Apply pending migrations
dotnet ef database update --project backend/src/StarterKit.Infrastructure --startup-project backend/src/StarterKit.API

# Add a new migration
dotnet ef migrations add <MigrationName> --project backend/src/StarterKit.Infrastructure --startup-project backend/src/StarterKit.API
```

In production, migrations are applied automatically on startup via `Database.MigrateAsync` unless `DatabaseSettings:MigrateOnStartup` is `false` — set it on every instance of a multi-instance deploy, and run the `migrate` command below once first, so instances starting together don't each run the same migration.

### One-shot CLI commands

```bash
# Apply pending migrations and exit (0 = done) — the deploy step when MigrateOnStartup=false
dotnet run --project backend/src/StarterKit.API -- migrate
# In the production image
dotnet StarterKit.API.dll migrate
```

`Program.cs` hands the arguments to `CliCommands.TryRunAsync` right after `Build()`: a first argument naming a registered `ICliCommand` (`backend/src/StarterKit.API/Commands/`) runs that command with the API's configuration and DI, in a scope of its own, and the process exits with its code — the web host, Hangfire and storage never start. Anything else (no arguments, host switches like `--urls`) starts the API as usual. New seed/import/admin commands implement `ICliCommand`, call Application services (the Infrastructure-only-in-`Program.cs` architecture rule applies to them too), and are added to `CliCommands.All`; exit code 1 means bad usage and prints the usage line.

### OpenAPI Spec Export

```bash
# Writes shared/openapi/openapi.json — no running server/DB required
dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true
```

Run after any controller/DTO change, then `bun run --cwd frontend codegen` — why it's opt-in and the full contract-sync workflow: `api-contract.md`.

The export builds the real host from `Program.cs` inside the `GetDocument.Insider` tool. `Program.cs` detects that (`isOpenApiExport`) and skips every startup step that reaches Postgres or S3 (migrations, Hangfire recurring-job registration, bucket creation) — any new startup step that touches an external service must go behind the same flag, or the export fails with connection errors that read like contract errors. With no database running, one `[ERR] ... connection to database` line from DataProtection's background key-ring load is still logged; it is harmless, the build succeeds.

### Tests

```bash
backend/scripts/test.sh
```

Builds the solution once (serialized, `-m:1` — see Build above), then runs the 4 test projects as independent parallel processes (each still `-m:1` internally, so no process attempts the broken parallel-build path — only the OS scheduler runs them concurrently) — markedly faster than `dotnet test backend/StarterKit.sln --no-restore -m:1` run serially. See `.claude/decisions.md` (2026-08-09) for why.

`STARTERKIT_TEST_REAL_DATA=1` also runs the `RealData` tests, `STARTERKIT_TEST_COVERAGE=1` collects coverage — see `testing.md`.

The plain solution-level command still works and is equivalent for correctness — use it if you need a single combined log/trx or are debugging the script itself:

```bash
dotnet test backend/StarterKit.sln --no-restore -m:1
```

For fast local iteration on a single layer, run just that project (skips the other 3 entirely):

```bash
dotnet test backend/tests/StarterKit.Domain.Tests --no-restore -m:1
```

Test-project layout, Testcontainers collections and test-host isolation: `testing.md` § Test infrastructure.

## Frontend (`frontend/`)

Package manager is `bun` (see `frontend/bun.lock`).

```bash
# Install deps
bun install --cwd frontend

# Dev server
bun run --cwd frontend dev

# Regenerate API client/types from shared/openapi/openapi.json (after a backend contract change)
bun run --cwd frontend codegen

# Type-check + production build
bun run --cwd frontend build

# Production build only — also writes the gitignored src/typings/, required before type-check
bun run --cwd frontend build-only

# Type-check only (vue-tsc over the app, node, vitest and e2e tsconfigs)
bun run --cwd frontend type-check

# Import-boundary lint (ESLint, layer rules only)
bun run --cwd frontend lint:boundaries

# Unit tests (vitest); test:coverage adds coverage
bun run --cwd frontend test:run
bun run --cwd frontend test:coverage

# E2E tests (playwright — install browsers once with test:e2e:install)
bun run --cwd frontend test:e2e

# Format (prettier) — ESLint runs only the layer-boundary rules, see frontend-conventions.md § Formatting & Linting
bun run --cwd frontend format
```

### User guide (`frontend/user-guide/site/`)

A separate VitePress package with its own `bun.lock` — see `frontend/user-guide/README.md` for authoring rules.

```bash
bun install --cwd frontend/user-guide/site
bun run --cwd frontend/user-guide/site dev        # http://localhost:5174/guide/
bun run --cwd frontend/user-guide/site test       # unit tests + content integrity (both languages, sidebar, images)
bun run --cwd frontend/user-guide/site build      # fails on dead links
bun run --cwd frontend/user-guide/site format     # prettier, using frontend/.prettierrc.json
bun run --cwd frontend/user-guide/site new-page <chapter>/<page> [--app]   # both language stubs from src/_template.md
bun run --cwd frontend guide:capture              # screenshots from the running app into site/src/*/images (needs user-guide/tools/.env + seeded data)
frontend/docker/verify-guide-access.sh            # Docker: builds the web image, checks private/public access with curl (SKIP_BUILD=1 reuses it)
```

Run the site's `dev` next to `bun run --cwd frontend dev` to follow the app header's "?" link: the app's dev server proxies `/guide` to port 5174 (no session check in dev). In the image, nginx serves the build at `/guide/`, private unless `GUIDE_PUBLIC=true` — see `frontend/user-guide/README.md` § Triển khai.
