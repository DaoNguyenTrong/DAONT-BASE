#!/usr/bin/env bash
# Re-exports shared/openapi/openapi.json, regenerates the frontend client from it and type-checks every
# caller — the contract-sync workflow in .claude/rules/api-contract.md as one command.
set -euo pipefail
cd "$(dirname "$0")/.."

dotnet build backend/src/StarterKit.API/StarterKit.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true
bun run --cwd frontend codegen
bun run --cwd frontend build-only
bun run --cwd frontend type-check

git diff --stat -- shared/openapi/openapi.json
