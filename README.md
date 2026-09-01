# StarterKit

An app starter kit for new web applications: .NET 10 Clean Architecture on the backend, Vue 3 + Vite on the frontend. It ships with the shared foundation most applications need — auth, multi-tenant organizations with per-org RBAC, API keys, audit log, file storage, system settings, notifications — so you can start writing business features right away instead of rebuilding basic infrastructure from scratch.

This is **not** a finished product — no business features are baked in. Remove/replace whatever you don't need and add your own domain on top of this foundation.

## Already Included

- **Auth**: email + password register/login (JWT access + refresh token, refresh token stored as a SHA-256 hash), mandatory email verification via SMTP, Google + Microsoft login (credential flow / MSAL popup), session management (list/revoke by device).
- **Profile**: self-service profile (get/update) and change password for the current account. There is no account CRUD/admin surface — accounts are created only via registration, and removed only by leaving/being removed from every organization.
- **Organizations & RBAC**: multi-tenant — an account can belong to multiple organizations and switches its active one, which is encoded as the JWT `org_id` claim. Access within an organization is governed by custom roles built from a fixed permission catalog; `Owner`/`Admin`/`Member` are seeded as system roles on creation (Owner gets every permission), but roles beyond that are fully organization-defined.
- **ApiKey**, **AuditLog**, **Files** (local disk, provider pluggable via `IStorageProvider`), **SystemSettings**: all four require an active organization *and* the specific permission for it — there is no personal/org-less access and no global admin bypass.
- **Notifications**: in-app (REST + real-time via SignalR), email, and optional Web Push (Firebase Cloud Messaging) — channels fan out through a Hangfire background job.

No global admin role anywhere in the system — outside of an organization ("personal" context) an account only has auth/profile/organization endpoints; add authorization as your specific application needs on top of the permission catalog.

## Architecture

```text
backend/    .NET 10 API — Domain → Application → Infrastructure → API (src/, tests/, StarterKit.sln)
frontend/   Vue 3 + Vite dashboard (src/api, src/stores, src/views, ...)
shared/     docs/ + openapi/ (shared contract — frontend generates its client/types from it)
plans/      history of plan files from implemented tasks (reference for patterns, not product documentation)
```

For layer/rule details for each part, see `CLAUDE.md` / `AGENTS.md` and `.claude/rules/`.

## Tech Stack

| Component       | Technology                                                                |
| --------------- | -------------------------------------------------------------------------- |
| Backend         | .NET 10, ASP.NET Core, Clean Architecture, EF Core                         |
| Database        | PostgreSQL                                                                  |
| Frontend        | Vue 3 + Vite, Pinia, naive-ui, vue-i18n, Tailwind                          |
| Storage         | Local disk currently; pluggable via `IStorageProvider`                     |
| Email           | SMTP (MailKit) — required, even in dev                                     |
| Background jobs | Hangfire (PostgreSQL storage, dashboard at `/hangfire`)                    |
| Realtime        | SignalR (`/hubs/notifications`, in-memory — no Redis backplane)            |
| Push            | Firebase Cloud Messaging (`FirebaseAdmin` SDK) — optional, no-op if unconfigured |
| Logging         | Serilog (structured, console + rolling file) + correlation ID middleware   |
| Monitoring      | Prometheus metrics (`/metrics`) + Grafana, see `monitoring/`               |

## Getting Started

### 1. Local Infrastructure (Postgres + Mailpit + Prometheus + Grafana)

```bash
docker compose up -d
```

Account verification email is required even in the dev environment — there is no seeder/bypass. Use Mailpit (`http://localhost:8025`) to view the verification email sent out when testing local registration, instead of needing a real SMTP server.

**Monitoring:** the API exposes Prometheus metrics at `/metrics` (`prometheus-net.AspNetCore`). Grafana (`http://localhost:3000`, `admin`/`admin`) comes pre-provisioned with a Prometheus datasource and a "StarterKit API Overview" dashboard (request rate/errors/latency, process CPU/memory, GC) — see `monitoring/`. Since the API isn't containerized, Prometheus reaches it via `host.docker.internal`, which requires running it bound to all interfaces instead of the default loopback-only bind:

```bash
dotnet run --project backend/src/StarterKit.API --urls http://0.0.0.0:7000
```

Without this, the Prometheus target reads as `down` and the Grafana dashboard stays empty. Binding to `0.0.0.0` also exposes the API to your LAN while it's running — fine for local monitoring, just don't leave it running unattended.

### 2. Backend (`backend/`)

```bash
# Copy the sample config
cp backend/src/StarterKit.API/appsettings.Example.json backend/src/StarterKit.API/appsettings.json

# Build — serialized, parallel build is broken in this .NET 10 environment
dotnet build backend/StarterKit.sln --no-restore -m:1

# Apply migrations
dotnet ef database update --project backend/src/StarterKit.Infrastructure --startup-project backend/src/StarterKit.API

# Run API
dotnet run --project backend/src/StarterKit.API

# Test
backend/scripts/test.sh
```

Production: migrations are applied automatically on startup via `Database.MigrateAsync`.

### 3. Frontend (`frontend/`)

Package manager: `bun`.

```bash
bun install --cwd frontend            # also generates src/api/generated/** via postinstall
bun run --cwd frontend dev            # dev server
bun run --cwd frontend build          # type-check + production build
bun run --cwd frontend test:run       # unit tests (vitest)
bun run --cwd frontend test:e2e:install && bun run --cwd frontend test:e2e   # e2e (playwright)
bun run --cwd frontend format         # prettier — there is no ESLint config in this project
```

### 4. Try the Registration Flow

`POST /api/auth/register` → open Mailpit (`localhost:8025`) → click the verification link → log in.

A freshly registered account has no organization yet, so Files/API keys/audit logs/system settings all return 403 until you create one: `POST /api/organizations` (or use the frontend's Organizations page), then `POST /api/auth/switch-organization` to make it the session's active organization — the creator is seeded as its Owner with every permission.

### Git Hooks (once, at the repo root)

```bash
bun install   # run at the repo root (not --cwd frontend) — wires up git hooks via Lefthook
```

Hooks installed:

- **pre-commit**: blocks direct commits to `main` (release only happens via `gh pr merge` — see the `git-release` skill; bypass with `git commit --no-verify` for tooling that legitimately needs it); blocks staged changes containing unresolved merge-conflict markers (`<<<<<<<`/`=======`/`>>>>>>>`); runs `secretlint` on all staged files (catches known secret *formats* — private keys, GitHub/Slack/npm tokens; it does **not** catch arbitrary custom secrets like a hardcoded `JwtSettings:SecretKey` or `DB_PASSWORD=...` — keep those in the gitignored `appsettings.json`, not in source); runs `prettier --write` on staged `frontend/src/**` files (same scope as `bun run --cwd frontend format`). Backend files aren't formatted (no `.editorconfig` exists yet to pin `dotnet format`'s behavior).
- **commit-msg**: enforces Conventional Commits (`feat:`, `fix:`, `docs:`, ... — see CONTRIBUTING.md) via commitlint.

### Updating the API Contract

Whenever you add or change a backend controller route, request/response DTO, or status code, regenerate the OpenAPI spec and the frontend client:

```bash
# 1. Backend: re-export the spec to shared/openapi/openapi.json (no running server/DB required)
dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true

# 2. Frontend: regenerate src/api/generated/** from the updated spec
bun run --cwd frontend codegen
```

- Step 1 writes `shared/openapi/openapi.json` — this file is committed (it's the single source of truth for the contract), so commit the diff along with your backend change.
- `OpenApiGenerateDocumentsOnBuild` defaults to `false`: generating the doc costs ~5-9s via a design-time host, so it's opt-in rather than slowing every plain `dotnet build`.
- Step 2 writes `frontend/src/api/generated/**` — gitignored, never hand-edited, and already regenerated automatically on every `bun install` via `postinstall`. Only needed here to pick up the change without reinstalling.
- There is no hand-written API wrapper layer on the frontend — views/stores call the generated client directly (side effects like updating auth state live in `frontend/src/stores/auth.ts`). See `.claude/rules/api-contract.md` for the full contract-sync workflow and how the generated client is wired in.

### CI

There is no CI workflow running build/test on PR/push yet (`.github/workflows/` only has `release.yml`, triggered on `v*` tags). Run the commands above locally before requesting a review.

For full details, see `.claude/rules/commands.md`.

## Production (Docker)

`docker-compose.prod.yml` builds and runs the API and frontend as containers (single-instance —
caching and SignalR are in-memory, don't scale `api` past 1 replica without pointing
`ConnectionStrings:Redis` at a Redis from your deploy environment first — this compose file no
longer ships a Redis container).
An nginx container serves the built frontend and reverse-proxies `/api/` and `/hubs/` to the API;
`/hangfire` and `/metrics` are deliberately not exposed through it, same "network-layer only"
restriction the API already applies (see `Program.cs`).

```bash
# 1. App secrets — same convention as local dev, just bind-mounted into the container instead of
#    baked into the image. Fill in real ConnectionStrings/JwtSettings/CorsSettings/EmailSettings/...
cp backend/src/StarterKit.API/appsettings.Example.json backend/src/StarterKit.API/appsettings.json

# 2. Compose-level vars (Postgres credentials, frontend build arg)
cp .env.prod.example .env.prod

# 3. Build and start
docker compose -f docker-compose.prod.yml --env-file .env.prod up -d --build
```

Migrations apply automatically on startup. Uploaded files and API logs persist in named volumes
(`starterkit-api-uploads`, `starterkit-api-logs`); Postgres data in `starterkit-postgres-data`.

Not included yet (fine for a first deploy, revisit before scaling): TLS termination at nginx, and
an `/health` endpoint for container healthchecks.

## Documentation

- Contributing / git workflow: [CONTRIBUTING.md](CONTRIBUTING.md)
- Code of Conduct: [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md)
- Security policy / reporting a vulnerability: [SECURITY.md](SECURITY.md)
- Changelog: [CHANGELOG.md](CHANGELOG.md)
- Key architecture decisions: [.claude/decisions.md](.claude/decisions.md)

## License

[MIT](LICENSE)
