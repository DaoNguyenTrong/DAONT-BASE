#!/usr/bin/env bash
# Brings up the local stack the Playwright suite and manual testing run against: Postgres, Mailpit and SeaweedFS
# in Docker, then the API on http://localhost:7100 with its database migrated and seeded (`seed-dev`). Safe to re-run.
# Logs and the API PID go to verify-output/stack/ (gitignored). Stop it with scripts/dev-down.sh.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

API_URL="http://localhost:7100"
STATE_DIR="$ROOT_DIR/verify-output/stack"
PID_FILE="$STATE_DIR/api.pid"
LOG_FILE="$STATE_DIR/api.log"

# seed-dev refuses to run outside Development; set it here rather than rely on launchSettings.json.
export ASPNETCORE_ENVIRONMENT=Development
# Overrides appsettings.json, which is per-machine and may point at another Postgres. These match docker-compose.yml.
export ConnectionStrings__DefaultConnection="Host=localhost;Port=5433;Database=StarterKit_api;Username=dao;Password=12345aA@"
# Vite dev server (5175) and the `vite preview` that Playwright serves (4173) both call this API.
export CorsSettings__AllowedOrigins__0="http://localhost:5175"
export CorsSettings__AllowedOrigins__1="http://localhost:4173"
# The default allows 5 sign-ins a minute per IP; the e2e suite and the verify scripts sign in far more often than that.
export RateLimiterSettings__AuthPermitLimit=1000

if ! command -v docker >/dev/null 2>&1; then
  echo "dev-up: docker is required (docker compose)." >&2
  exit 1
fi

mkdir -p "$STATE_DIR"

docker compose up -d --wait postgres mailpit seaweedfs

dotnet run --project backend/src/StarterKit.API -- migrate
dotnet run --project backend/src/StarterKit.API -- seed-dev

if curl -fsS "$API_URL/api/health" >/dev/null 2>&1; then
  echo "dev-up: API already answers on $API_URL."
  exit 0
fi

# --no-build: the two runs above already built it. The host switch comes after `--` so dotnet passes it to the app.
nohup dotnet run --no-build --project backend/src/StarterKit.API -- --urls "$API_URL" >"$LOG_FILE" 2>&1 &
echo $! >"$PID_FILE"

for _ in $(seq 1 60); do
  if curl -fsS "$API_URL/api/health" >/dev/null 2>&1; then
    echo "dev-up: API is up on $API_URL (log: $LOG_FILE)."
    exit 0
  fi
  if ! kill -0 "$(cat "$PID_FILE")" 2>/dev/null; then
    echo "dev-up: the API exited during start-up. See $LOG_FILE" >&2
    exit 1
  fi
  sleep 1
done

echo "dev-up: the API did not answer on $API_URL within 60s. See $LOG_FILE" >&2
exit 1
