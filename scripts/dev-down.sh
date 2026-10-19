#!/usr/bin/env bash
# Stops what scripts/dev-up.sh started: the API process and the Docker services. Volumes are kept, so the data
# survives; a re-run of dev-up.sh reuses it. Safe to re-run.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

PID_FILE="$ROOT_DIR/verify-output/stack/api.pid"

if [ -f "$PID_FILE" ]; then
  pid="$(cat "$PID_FILE")"
  if kill -0 "$pid" 2>/dev/null; then
    kill "$pid"
    echo "dev-down: stopped the API (pid $pid)."
  fi
  rm -f "$PID_FILE"
fi

if command -v docker >/dev/null 2>&1; then
  docker compose stop postgres mailpit seaweedfs
fi
