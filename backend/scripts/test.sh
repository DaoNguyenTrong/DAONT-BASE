#!/usr/bin/env bash
# Runs the backend test suite: build the solution once (serialized — parallel MSBuild
# is broken in this .NET 10 environment, see commands.md), then run the 4 test
# projects as independent OS processes in parallel. Each `dotnet test` call still
# passes -m:1, so no single process attempts the parallel-build path that's broken —
# only the OS scheduler runs the 4 already-built processes concurrently.
#
# Measured effect (479 tests, 2026-08-09): 55.8s serial (`dotnet test
# backend/StarterKit.sln --no-restore -m:1`) -> 29.7s with this script.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

dotnet build backend/StarterKit.sln --no-restore -m:1

projects=(
  backend/tests/StarterKit.Domain.Tests/StarterKit.Domain.Tests.csproj
  backend/tests/StarterKit.Application.Tests/StarterKit.Application.Tests.csproj
  backend/tests/StarterKit.Infrastructure.Tests/StarterKit.Infrastructure.Tests.csproj
  backend/tests/StarterKit.API.Tests/StarterKit.API.Tests.csproj
)

pids=()
for project in "${projects[@]}"; do
  dotnet test "$project" --no-build --no-restore -m:1 &
  pids+=("$!")
done

status=0
for pid in "${pids[@]}"; do
  wait "$pid" || status=1
done

exit "$status"
