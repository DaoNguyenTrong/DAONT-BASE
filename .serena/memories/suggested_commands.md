# Suggested Commands

## Local dev bootstrap
```bash
docker compose up -d   # Postgres + Mailpit
cp backend/src/StarterKit.API/appsettings.Example.json backend/src/StarterKit.API/appsettings.json
dotnet ef database update --project backend/src/StarterKit.Infrastructure --startup-project backend/src/StarterKit.API
```

## Backend (`backend/`)
```bash
# Build — serialized, parallel build is broken in this .NET 10 env
dotnet build backend/StarterKit.sln --no-restore -m:1

# Run API
dotnet run --project backend/src/StarterKit.API

# Test
dotnet test backend/StarterKit.sln --no-restore -m:1

# EF Core migrations
dotnet ef database update --project backend/src/StarterKit.Infrastructure --startup-project backend/src/StarterKit.API
dotnet ef migrations add <Name> --project backend/src/StarterKit.Infrastructure --startup-project backend/src/StarterKit.API
```

## Frontend (`frontend/`)
```bash
bun install --cwd frontend
bun run --cwd frontend dev
bun run --cwd frontend build          # type-check + production build
bun run --cwd frontend test:run       # vitest
bun run --cwd frontend test:e2e:install && bun run --cwd frontend test:e2e
bun run --cwd frontend format         # prettier — no ESLint in this project
```

## Code intelligence
```bash
npx gitnexus analyze   # re-index after structural changes (rename, delete, move)
```
GitNexus MCP tools (`gitnexus_query`, `gitnexus_context`, `gitnexus_impact`, `gitnexus_detect_changes`)
for orientation/impact analysis; Serena MCP tools (`find_symbol`, `find_referencing_symbols`,
`get_symbols_overview`) for backend C# symbol-level work — see `.claude/rules/serena.md` for scope
(backend only until a TS/Vue language server is added).
