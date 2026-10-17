# Renaming This Project

This repo ships under the name `StarterKit`. A fork renames it once, right after cloning, with
`scripts/rename.sh`. (The repo was itself renamed once before — see
`plans/implements/07. Turn FeedbackHub into a generic app starter.md` for that one-off pass.)

## Quick start

```bash
scripts/rename.sh --dry-run Acme   # list the path moves and the files it would rewrite
scripts/rename.sh Acme             # do it (refuses to run with uncommitted tracked changes)
```

The new name is PascalCase; every other form is derived from it:

| Form       | Current       | `Acme` / `CimpVbi`  | Used in                                                                                                                                                                                         |
| ---------- | ------------- | ------------------- | ----------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------------- |
| PascalCase | `StarterKit`  | `Acme` / `CimpVbi`  | C# namespaces, `.sln`/`.csproj` names and paths (`backend/Dockerfile` too), DB name `StarterKit_api`, JWT `Issuer`/`Audiences`, `EmailSettings.FromName`, DataProtection purpose, OpenAPI title |
| camelCase  | `starterKit`  | `acme` / `cimpVbi`  | `frontend/orval.config.ts` project key                                                                                                                                                          |
| lowercase  | `starterkit`  | `acme` / `cimpvbi`  | root `package.json` name, storage bucket, `POSTGRES_USER`, Docker volumes, Prometheus job, Grafana dashboard (file, `uid`, queries), test databases and temp dirs                               |
| kebab-case | `starter-kit` | `acme` / `cimp-vbi` | `frontend/package.json` name (and `frontend/bun.lock`)                                                                                                                                          |
| UPPERCASE  | `STARTERKIT`  | `ACME` / `CIMPVBI`  | nothing today — replaced if it appears                                                                                                                                                          |

What the script does:

1. `git mv` every tracked path carrying the name (project folders move whole, so their gitignored
   `bin/`, `obj/` and `appsettings.json` move along).
2. Rewrites every exact form above in every tracked text file — no extension filter, so
   `backend/Dockerfile`, `bun.lock` and the EF migration snapshots are covered.
3. Lists the gitignored `appsettings*.json` / `.env*` that still name the old project
   (`--include-local` rewrites them too).
4. Fails if `git grep` still finds an exact form, then lists prose mentions ("starter kit") for you
   to review — they describe the template and are left alone.

Left as written: `plans/`, `CHANGELOG.md`, `.claude/decisions.md`, this file and the script — they
are history. A fork renaming itself again passes `--from Acme NewName`. Two limits: the new name
must not contain the old one (`StarterKitPro` — rename through a temporary name), and a one-word old
name loses its case forms (`acme` was both `starterkit` and `starter-kit`), so each occurrence
becomes the lowercase new form (`newname`, also for package names).

## After the script

1. Rebuild and test (stale `bin/`/`obj/` still name the old projects):
   ```bash
   rm -rf backend/src/*/bin backend/src/*/obj backend/tests/*/bin backend/tests/*/obj
   dotnet restore backend/Acme.sln && backend/scripts/test.sh
   bun install && bun install --cwd frontend && bun run --cwd frontend test:run && bun run --cwd frontend build
   ```
2. Final check — must list only the history files above and prose:
   ```bash
   git grep -nIi 'starter.\?kit'
   ```
3. Every other developer redoes their gitignored config after pulling — a pull moves only tracked
   files, so `appsettings.json` stays behind in `backend/src/StarterKit.API/`. On the renamed
   checkout, `scripts/rename.sh --include-local Acme` touches nothing tracked: it rewrites the local
   `appsettings*.json` / `.env*` and moves them into the renamed project folder. Then delete the
   leftover `backend/src/StarterKit.*` / `backend/tests/StarterKit.*` folders (only `bin/`/`obj/`
   remain).
4. The user guide: the script already renames `name` in
   `frontend/user-guide/site/.vitepress/product.ts`; set `publisher` and the logo file names
   (`brand`, files in `frontend/public/`) there by hand, and rewrite the home pages
   `frontend/user-guide/site/src/index.md` and `src/en/index.md`. Those are the guide's fork-owned
   files — base-owned files (`src/base/**`, `theme/**`, `sidebar.base.ts`) stay as they are so later
   guide updates from the base merge cleanly (see `frontend/user-guide/README.md`).
5. CodeGraph re-indexes on its own (file watcher); restart Serena so its language servers load the
   new project paths.
5. Smoke test: register → verification email in Mailpit (`localhost:8025`) → verify → log in.

## Caveats — runtime identifiers

On a fresh fork none of this matters. On anything already deployed, each of these changes behaviour
the moment it ships — to keep an old value, revert that line after running the script.

- **Database name** (`StarterKit_api` in `ConnectionStrings.DefaultConnection`, `POSTGRES_DB`) and
  **`POSTGRES_USER`**: renaming the string doesn't rename the database, and the Postgres image reads
  `POSTGRES_*` only when it initialises an empty volume. Either `ALTER DATABASE "StarterKit_api"
RENAME TO "Acme_api";` (and keep the old user) everywhere it exists, or start from an empty
  database.
- **Docker volumes** (`starterkit-postgres-data`, `-api-uploads`, `-api-logs`, `-seaweedfs-data`):
  a renamed volume is a **new, empty volume** — the database and uploaded files stay behind in the
  old one. Keep the old names, or copy the data across before switching
  (`docker run --rm -v old:/from -v new:/to alpine cp -a /from/. /to/`).
- **Storage bucket** (`starterkit`): the new bucket starts empty; stored files stay in the old one.
- **JWT `Issuer` / `Audiences`**: validated on every request — every issued access and refresh token
  stops working and every user signs in again.
- **DataProtection purpose** (`"StarterKit.Secrets"`, `DataProtectionSecretProtector`): values
  protected under the old purpose can no longer be read. Nothing in the template stores any yet —
  it matters once a fork persists `ISecretProtector` output.
- **Prometheus job** (`starterkit-api`) and the Grafana dashboard `uid`: metrics under the new job
  name start a new series — history stays under the old label, and bookmarks to the old dashboard
  URL break.

## Fork checklist (beyond the name)

- **Git host** — GitHub is the default. On another host (e.g. GitLab):
  - `.claude/release-kit.json`: `prCli` (`gh` → `glab`) and `ciTriggerNote`.
  - `lefthook.yml` `branch-guard`: the hint names `gh pr merge`.
  - `CONTRIBUTING.md`: "create PR to `dev` on GitHub", "GitHub Actions creates the release".
  - CI: `.gitlab-ci.yml` is the GitLab twin of `.github/workflows/release.yml` — delete the one
    you don't use. On GitLab the runner needs Docker (`docker:dind`, privileged — see the file).
  - Protect `v*` tags so only maintainers can push a release.
- **`SECURITY.md`** — the reporting link and email point at this template's maintainer.
- **`.serena/project.yml`** `project_name` — tracks the folder name (`DAONT-BASE`), not the code
  name; change it only if the folder is renamed.
- **`CHANGELOG.md`** — start the fork's own history under `[Unreleased]`.
