# Renaming This Project

This repo currently ships under the name `StarterKit`. It was itself renamed once before (from
a prior product name), and that pass is the basis for this guide — see
`plans/implements/07. Turn FeedbackHub into a generic app starter.md` for the original, one-off
version of this exact procedure. This document generalizes it so the next rename doesn't require
re-deriving the mapping from scratch.

## Naming forms currently in use

| Form                | Current value                | Used in                                                                 |
| -------------------- | ----------------------------- | ------------------------------------------------------------------------- |
| PascalCase            | `StarterKit`                  | C# namespaces, project/solution file names & content, JWT `Issuer`/`Audiences`, `EmailSettings.FromName`, OpenAPI doc title |
| PascalCase + suffix   | `StarterKit_api`              | Postgres database name (connection string, `docker-compose.yml`)         |
| kebab-case            | `starter-kit`                 | `frontend/package.json` `"name"`                                        |

Pick the equivalent three forms for the new name before starting (e.g. `Acme` / `Acme_api` / `acme`).

## Scope (as of this writing)

Grep for the exact current counts before you start — they drift as the codebase grows:

```bash
grep -rlI "StarterKit" --include=*.* . --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git --exclude-dir=dist | wc -l
find . -iname "*StarterKit*" -not -path "*/node_modules/*" -not -path "*/bin/*" -not -path "*/obj/*" -not -path "*/.git/*"
```

At last check: **225 files** contain the string, **17 physical paths** have it in the name.

### 1. Backend — physical paths (`git mv`, not delete+recreate)

- `backend/StarterKit.sln`
- `backend/src/StarterKit.{Domain,Application,Infrastructure,API}/` + each folder's `.csproj`
- `backend/tests/StarterKit.{Domain,Application,Infrastructure,API}.Tests/` + each folder's `.csproj`

### 2. Backend — text (namespaces, usings, project references, EF migration snapshots)

Every `namespace StarterKit.*;` / `using StarterKit.*;` declaration, every `<ProjectReference>` in
the `.csproj` files, and the entity-type strings baked into
`Infrastructure/Migrations/*.Designer.cs` + `AppDbContextModelSnapshot.cs` (e.g.
`"StarterKit.Domain.Entities.Account"`). These are plain string literals EF uses for model
comparison — safe to bulk text-replace as long as every occurrence is replaced consistently.
Also: `Services/DataProtectionSecretProtector.cs` (`"StarterKit.Secrets"` protector purpose
string — changing it invalidates any data already protected under the old purpose string) and
`Extensions/OpenApiExtensions.cs` (`document.Info.Title`).

### 3. Config values (semantic, not just text — see Caveats)

- `backend/src/StarterKit.API/appsettings.Example.json` **and** the gitignored local
  `appsettings.json`: `ConnectionStrings.DefaultConnection` DB name, `JwtSettings.Issuer`,
  `JwtSettings.Audiences`, `EmailSettings.FromName`.
- `docker-compose.yml`: `POSTGRES_DB`.

### 4. Frontend

- `frontend/package.json` `"name"`.
- `frontend/README.md` title.
- (Frontend has no other naming coupling — `index.html`/locales already say the generic "App
  Starter", not `StarterKit`.)

### 5. CI

- `.github/workflows/release.yml` — `.sln` path in the restore/test steps.

### 6. `.claude/` tooling

- `rules/{architecture,commands}.md` and any other rule file with a `backend/StarterKit.sln` /
  `dotnet test` command example.
- `hooks/pre-task-reminder.sh` — same `.sln` path.
- `skills/clean-architecture-review/{SKILL.md,violation-patterns.md}`,
  `skills/crud-entity/SKILL.md` — check for hardcoded `StarterKit.*` namespace examples.
- `decisions.md` — only if an entry names the old project name as prose; don't touch entries just
  because they mention a namespace incidentally.

### 7. Root & shared docs

- `README.md`, `CONTRIBUTING.md`, `CHANGELOG.md`, `CLAUDE.md`, `AGENTS.md` — title + `.sln`/path
  references.
- `shared/openapi/README.md`, `shared/openapi/openapi.json` (`info.title`/`info.description`).

## Procedure (least-effort order)

1. **Pick the new name's three case forms** (see table above).
2. **`git mv` the 17 physical paths** first, so the working tree matches the new layout before any
   text edit touches file contents (avoids editing a file and then moving it).
3. **Bulk text-replace**, scoped to tracked source (skip `bin/`, `obj/`, `node_modules/`, `dist/`
   — these regenerate on next build):
   ```bash
   grep -rlI "StarterKit" --include=*.* . --exclude-dir=node_modules --exclude-dir=bin --exclude-dir=obj --exclude-dir=.git --exclude-dir=dist \
     | xargs sed -i 's/StarterKit/NewName/g'
   ```
   Run the same for the `starter-kit` (kebab-case) hits separately — don't conflate the two cases
   in one regex, they land in different files with different casing rules.
4. **Manually update the gitignored `appsettings.json`** (bulk replace above only touches it if it
   already exists on this machine — every other developer must redo this locally after pulling).
5. `dotnet build backend/{NewName}.sln --no-restore -m:1` — let the compiler catch anything the
   grep-based sweep missed (stale namespace, mismatched project reference name).
6. `dotnet test backend/{NewName}.sln --no-restore -m:1` and `bun run --cwd frontend test:run` +
   `bun run --cwd frontend build`.
7. `npx gitnexus analyze` to refresh the index against the new namespaces, then
   `gitnexus_detect_changes(scope: "all")` to confirm only the expected symbols moved and nothing
   was accidentally broken.
8. Manual smoke test: register → check the verification email in Mailpit (`localhost:8025`) →
   verify → log in.

## Caveats — do not blindly bulk-replace these without a decision

- **Database name** (`ConnectionStrings.DefaultConnection` / `docker-compose.yml`
  `POSTGRES_DB`): renaming the string doesn't rename an existing database. Either
  `ALTER DATABASE "StarterKit_api" RENAME TO "NewName_api";` on every environment that has one, or
  accept that local/dev databases get recreated from scratch. Coordinate before touching anything
  with real data.
- **`JwtSettings.Issuer` / `Audiences`**: these are validated on every request. Changing them
  invalidates every access and refresh token issued under the old values — every logged-in user is
  forced to re-authenticate the moment this ships. Fine for a fresh project with no real users;
  needs a deliberate rollout plan otherwise.
- **`DataProtectionSecretProtector`'s purpose string** (`"StarterKit.Secrets"`): changing it means
  any secret already protected under the old purpose string can no longer be unprotected. Only
  matters if something has already been encrypted in a persisted environment.
- **GitNexus's repo id** (`DAONT-BASE` — see `gitnexus://repo/DAONT-BASE/...` resource URIs
  throughout `.claude/skills/gitnexus/`) tracks the **folder name**, not the code namespace. A
  code-name-only rename (`StarterKit` → `NewName`) does **not** require touching these URIs; they
  only change if the repo's folder itself is renamed.
