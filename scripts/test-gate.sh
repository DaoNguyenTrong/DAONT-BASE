#!/usr/bin/env bash
# Runs locally the same checks as CI's test gate (.github/workflows/test.yml), minus coverage — the
# gate /git-release runs before it merges and tags (.claude/release-kit.json), and the one to run
# before pushing. Change it together with test.yml so a release can't pass here and fail on the tag.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

example_settings=backend/src/StarterKit.API/appsettings.Example.json
appsettings=backend/src/StarterKit.API/appsettings.json
temp_appsettings=0

# The OpenAPI export needs a readable appsettings.json, copied from the example when absent; without one it can
# only fail, so stop before the slow steps. A dangling symlink is unusable and also blocks the noclobber copy.
if [[ -L "$appsettings" && ! -e "$appsettings" ]]; then
  echo "test-gate: $appsettings is a symlink to a missing file — fix its target or remove it." >&2
  exit 1
fi
if [[ ! -f "$example_settings" && ! -f "$appsettings" ]]; then
  echo "test-gate: $example_settings and $appsettings are both missing — restore the example from git" \
    "(git checkout -- $example_settings) or create your own $appsettings." >&2
  exit 1
fi

# Deletes the temporary appsettings.json only while it still equals the example: a gitignored file
# the developer wrote meanwhile has no other copy. Must return 0 — it runs under set -e.
remove_temp_appsettings() {
  ((temp_appsettings)) || return 0
  temp_appsettings=0
  if [[ ! -e "$appsettings" && ! -L "$appsettings" ]]; then return 0; fi
  if [[ -f "$appsettings" && ! -L "$appsettings" ]] && cmp -s "$example_settings" "$appsettings"; then
    rm -f -- "$appsettings"
  else
    echo "test-gate: kept $appsettings — it changed after the gate copied it there, so it may be your own config." >&2 || true
  fi
}

# One EXIT trap for every temp file; HUP/INT/TERM exit through it, so a closed terminal or Ctrl-C cleans up too.
temp_files=()
cleanup() {
  remove_temp_appsettings
  if ((${#temp_files[@]})); then rm -f -- "${temp_files[@]}"; fi
}
trap cleanup EXIT
trap 'exit 129' HUP
trap 'exit 130' INT
trap 'exit 143' TERM

# Comment length (.claude/rules/comments.md) — seconds, so it fails fast.
bun test scripts/
bun scripts/check-comments.ts

backend/scripts/test.sh

# A controller/DTO change without a re-export leaves the frontend's generated client stale. Compared
# against the file as it was before the export, not against git (CI's check), so an export that is
# re-run but not committed yet still passes locally.
spec=shared/openapi/openapi.json
before="$(mktemp)"
temp_files+=("$before")
cp "$spec" "$before"
# The export dies without appsettings.json (CorsSettings); the tests pin the example's keys as env vars and don't.
# An existing file is per-machine and never touched: noclobber creates the copy only if no file is there.
if (set -C && cat "$example_settings" >"$appsettings") 2>/dev/null; then
  temp_appsettings=1
  echo "test-gate: $appsettings missing — using a temporary copy of appsettings.Example.json."
fi
dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true
remove_temp_appsettings
if ! cmp -s "$before" "$spec"; then
  echo "$spec was out of date and has been re-exported — review and commit it (commands.md § OpenAPI Spec Export)." >&2
  exit 1
fi

bun run --cwd frontend prettier --check --experimental-cli src/ e2e/
# src/typings/ is written by the Vite plugins, complete only after a build — a fresh clone or
# worktree has none, and vue-tsc fails without it.
bun run --cwd frontend build-only
bun run --cwd frontend type-check
bun run --cwd frontend lint:boundaries
bun run --cwd frontend test:run

# The user guide: formatting, unit + content integrity tests, and a build that fails on dead links.
bun install --cwd frontend/user-guide/site --frozen-lockfile
bun run --cwd frontend/user-guide/site format:check
bun run --cwd frontend/user-guide/site test
bun run --cwd frontend/user-guide/site build
