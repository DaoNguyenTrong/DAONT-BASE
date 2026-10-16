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

In production, migrations are applied automatically on startup via `Database.MigrateAsync`.

### OpenAPI Spec Export

```bash
# Writes shared/openapi/openapi.json — no running server/DB required
dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true
```

Off by default on plain `dotnet build` (costs ~5-9s via a design-time host). Run after any controller/DTO change, then regenerate the frontend client (see below). See `.claude/rules/api-contract.md` for the full contract-sync workflow.

The export builds the real host from `Program.cs` inside the `GetDocument.Insider` tool. `Program.cs` detects that (`isOpenApiExport`) and skips every startup step that reaches Postgres or S3 (migrations, Hangfire recurring-job registration, bucket creation) — any new startup step that touches an external service must go behind the same flag, or the export fails with connection errors that read like contract errors. With no database running, one `[ERR] ... connection to database` line from DataProtection's background key-ring load is still logged; it is harmless, the build succeeds.

### Tests

```bash
backend/scripts/test.sh
```

Builds the solution once (serialized, `-m:1` — see Build above), then runs the 4 test projects as independent parallel processes (each still `-m:1` internally, so no process attempts the broken parallel-build path — only the OS scheduler runs them concurrently). Measured 2026-08-09: 479 tests, ~35s, vs. ~56s for `dotnet test backend/StarterKit.sln --no-restore -m:1` run serially. See `.claude/decisions.md` (2026-08-09) for why.

The plain solution-level command still works and is equivalent for correctness — use it if you need a single combined log/trx or are debugging the script itself:

```bash
dotnet test backend/StarterKit.sln --no-restore -m:1
```

For fast local iteration on a single layer, run just that project (skips the other 3 entirely):

```bash
dotnet test backend/tests/StarterKit.Domain.Tests --no-restore -m:1
```

Test projects live under `backend/tests/` (`StarterKit.Domain.Tests`, `StarterKit.Application.Tests`, `StarterKit.Infrastructure.Tests`, `StarterKit.API.Tests`), mirroring the `backend/src/` layer split. `Infrastructure.Tests` and `API.Tests` each spin up their own `Testcontainers.PostgreSql` container — this is why they dominate total runtime (~8s and ~13s respectively, measured 2026-10-16, mostly container start-up) while `Domain.Tests`/`Application.Tests` are near-instant.

`Infrastructure.Tests` starts **one** Postgres container per process and migrates a template database once (`PostgresContainerFixture`); each xUnit collection (`PostgresCollection`, `PostgresCollectionB`) clones its own database from it (`CREATE DATABASE … TEMPLATE`), so collections run in parallel without seeing each other's data. A new Postgres-backed class that rolls back every test (`BeginTransactionAsync` in `InitializeAsync`) goes in `PostgresCollectionB`; one that commits rows or changes the schema goes in `PostgresCollection`. When one collection grows slow, add another `ICollectionFixture<PostgresContainerFixture>` definition and move classes over by measured time. Cleanup relies on Testcontainers' Ryuk — don't set `TESTCONTAINERS_RYUK_DISABLED`. The template migration runs with `Pooling=false`: an idle pooled connection left on the template makes every clone fail with `55006`.

### Test-host conventions (keep the suite fast and isolated)

- **Password hashing**: `ApiFactoryFixture` registers the real `PasswordHasher` at BCrypt cost 4 (`FastPasswordHasher.Create()`); production stays at 12, pinned by `PasswordHasherTests`. Seed hashes with `FastPasswordHasher.WorkFactor`, not a literal 12 — a cost-12 seed still verifies, it's just ~0.3s slower each time.
- **DB reset**: Respawn runs `DELETE`, not `TRUNCATE` (~60ms saved per reset). `DELETE` doesn't reset sequences — never assert a specific auto-increment id. The `hangfire` schema is excluded (wiping it makes every `WithWebHostBuilder` host reinstall the schema and hang).
- **No dev services**: the test host pins `CacheSettings__Provider=Memory` and `RealtimeSettings__Backplane=None` — Respawn only resets Postgres, so a Redis on the dev machine would carry state between runs.

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

# Unit tests (vitest)
bun run --cwd frontend test:run

# E2E tests (playwright — install browsers once with test:e2e:install)
bun run --cwd frontend test:e2e

# Format (prettier — there is no ESLint config in this project; do not add one without confirming with the user)
bun run --cwd frontend format
```

### Frontend test conventions

- `tests/setup.ts` unmounts every wrapper after each test (`enableAutoUnmount`) so component timers can't fire into the next test. Keep it free of heavy imports — it runs once per test file; that's why the confirmation override lives in `lib/confirmation-override.ts`, not in naive-ui-backed `lib/feedback-naive.ts`.
- Read a store after `renderComponent` with `storeIn(pinia, useXxxStore)` (`tests/helpers/pinia.ts`), using the `pinia` it returns — a testing pinia ignores `useXxxStore(pinia)`'s argument and returns the *active* pinia's store, which a leftover timer from an earlier test may have switched.
- Tests that drive the real router (guards, route table) stub the lazy views they navigate to with `vi.mock('@/views/XxxView.vue', () => stubView())` (`tests/helpers/router-view-stubs.ts`) instead of loading real view chunks.
- `type-check` runs `src` under both `tsconfig.app.json` and `tsconfig.vitest.json` on purpose: the vitest config adds `node`/`vitest/globals` types, so only the app pass catches app code that leans on them.

## Git Hooks

Managed by [Lefthook](https://lefthook.dev) — config in root `lefthook.yml`, not `.husky/`.

```bash
# Run once at the repo root (not --cwd frontend) — installs git hooks (prepare script: lefthook install)
bun install
```

- **pre-commit**: `piped: true` — stops at the first failing command instead of running the rest. Blocks direct commits to `main` (bypass: `git commit --no-verify` — needed by nothing in the documented workflow except tooling that intentionally commits there); blocks staged merge-conflict markers; runs `secretlint` on all staged files (known secret *formats* only — private keys, GitHub/Slack/npm tokens; does not catch arbitrary custom secrets); runs `prettier --write` on staged `frontend/src/**` and `frontend/e2e/**` files (mirrors `bun run --cwd frontend format`'s scope). No backend formatting hook yet (no `.editorconfig` to pin `dotnet format` behavior).
- **commit-msg**: commitlint, Conventional Commits (`@commitlint/config-conventional`).

Note: `dev` and `release/*` are intentionally **not** blocked by the branch guard — the `git-release` skill (from the `release-kit` plugin; bindings in `.claude/release-kit.json`) commits the CHANGELOG bump directly to `dev` before cutting a `release/vX.Y.Z` branch (Phase 1), then opens the release PR from `release/vX.Y.Z` to `main` once QA stabilization is done (Phase 2). See `CONTRIBUTING.md` § Release Process.

Glob gotcha if you edit `lefthook.yml`: Lefthook's glob matcher (`gobwas/glob`) treats `dir/**/*` as requiring **at least one** intermediate directory — it silently skips files directly in `dir/` (e.g. `frontend/src/main.ts` under a `frontend/src/**/*` glob). Verified empirically (not from docs) while wiring this up. Match both depths with two patterns (see `prettier`'s glob list in `lefthook.yml`), or use a bare `**` when there's no path prefix to anchor (see `secretlint`'s glob).

## CI

There is **no CI workflow that runs build/test on PR or push today** — nothing blocks a merge on a failing build or test yet; run the commands above locally before asking for review.

The only pipeline is the release, fired by the `v*` tag the `git-release` skill (from the `release-kit` plugin) pushes: test → build and push both production images → create the release. `.github/workflows/release.yml` (images to GHCR) and `.gitlab-ci.yml` (images to the project's Container Registry) are the same pipeline for the two hosts; change both together. Images are built from the tag's pipeline, never from a push to `main`: `git-release` merges into `main` before it tags, so a `main` build can't see the tag and MinVer/`git describe` would report `X.Y.(Z+1)-preview`. Both need full history (`fetch-depth: 0` / `GIT_DEPTH: "0"`), and `v*` tags must be protected.
