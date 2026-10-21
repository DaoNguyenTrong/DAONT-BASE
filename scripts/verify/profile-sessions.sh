#!/usr/bin/env bash
# Checks the "Profile and login sessions" feature (docs/feature-map.md) against the running API: two sign-ins make two
# sessions, the current one is marked, and "sign out other devices" leaves only the current one, still signed in.
# Two kinds of client are checked. A browser keeps a cookie jar per device, and its current session is the one its
# requests name. A Bearer-only client has no cookies: the session it is in is the one its access token names (the
# session_id claim, see SessionService), and the same rules must hold for it.
# Needs scripts/dev-up.sh first, plus curl and jq. Responses are saved under verify-output/profile-sessions/.
# Exit 0 when every check passes, 1 otherwise.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"

API_URL="${API_URL:-http://localhost:7100}"
USERNAME="${E2E_USER_USERNAME:-e2e-user}"
PASSWORD="${E2E_USER_PASSWORD:-E2e-User!2026}"
EVIDENCE_DIR="$ROOT_DIR/verify-output/profile-sessions"

for tool in curl jq; do
  if ! command -v "$tool" >/dev/null 2>&1; then
    echo "profile-sessions: $tool is required." >&2
    exit 1
  fi
done

mkdir -p "$EVIDENCE_DIR"
rm -f "$EVIDENCE_DIR"/*.json "$EVIDENCE_DIR"/*.jar
failures=0

# call <label> <device-jar> <method> <path> [json-body]: stores the HTTP status in $status, the body in <label>.json.
# The X-CSRF-Protection header is what the SPA sends with every cookie-authenticated write (CsrfProtectionMiddleware).
call() {
  local label="$1" jar="$2" method="$3" path="$4" body="${5:-}"
  local args=(-sS -o "$EVIDENCE_DIR/$label.json" -w '%{http_code}' -X "$method" "$API_URL$path"
    -c "$jar" -b "$jar" -H 'X-TimeZone: Asia/Ho_Chi_Minh' -H 'X-CSRF-Protection: 1')
  if [ -n "$body" ]; then
    args+=(-H 'Content-Type: application/json' -d "$body")
  fi
  status="$(curl "${args[@]}")"
}

# check <description> <expected-status>: records a PASS or FAIL for the last call.
check() {
  if [ "$status" = "$2" ]; then
    echo "PASS  $1 (HTTP $status)"
  else
    echo "FAIL  $1 (expected HTTP $2, got $status)"
    failures=$((failures + 1))
  fi
}

# expect <description> <jq-filter> <file>: records a PASS when the filter holds for the saved response.
expect() {
  if jq -e "$2" "$3" >/dev/null 2>&1; then
    echo "PASS  $1"
  else
    echo "FAIL  $1"
    failures=$((failures + 1))
  fi
}

login_body="{\"username\":\"$USERNAME\",\"password\":\"$PASSWORD\"}"
other_jar="$EVIDENCE_DIR/other.jar"
current_jar="$EVIDENCE_DIR/current.jar"

call login-other "$other_jar" POST /api/auth/login "$login_body"
check "sign in on the other device" 200

call login-current "$current_jar" POST /api/auth/login "$login_body"
check "sign in on the current device" 200

call profile-before "$current_jar" GET /api/profile
check "GET /api/profile returns the signed-in account" 200
expect "profile belongs to $USERNAME" ".username == \"$USERNAME\"" "$EVIDENCE_DIR/profile-before.json"

call sessions-before "$current_jar" GET /api/auth/sessions
check "GET /api/auth/sessions lists both sessions" 200
expect "at least two sessions are listed" "length >= 2" "$EVIDENCE_DIR/sessions-before.json"
expect "exactly one session is marked as current" "[.[] | select(.isCurrent)] | length == 1" "$EVIDENCE_DIR/sessions-before.json"

call revoke-others "$current_jar" POST /api/auth/sessions/revoke-others
check "POST /api/auth/sessions/revoke-others" 204

call sessions-after "$current_jar" GET /api/auth/sessions
check "GET /api/auth/sessions after revoking the others" 200
expect "only the current session is left, and it is marked" "length == 1 and .[0].isCurrent" "$EVIDENCE_DIR/sessions-after.json"

call profile-after "$current_jar" GET /api/profile
check "the current session is still signed in" 200

# Bearer-only client: no cookies, so the session must come from the access token alone.
call login-bearer "$EVIDENCE_DIR/bearer.jar" POST /api/auth/login "$login_body"
check "sign in as a Bearer-only client" 200
bearer_token="$(jq -r '.accessToken' "$EVIDENCE_DIR/login-bearer.json")"
rm -f "$EVIDENCE_DIR/bearer.jar"

call_bearer() {
  local label="$1" token="$2" method="$3" path="$4"
  status="$(curl -sS -o "$EVIDENCE_DIR/$label.json" -w '%{http_code}' -X "$method" "$API_URL$path" \
    -H 'X-TimeZone: Asia/Ho_Chi_Minh' -H "Authorization: Bearer $token")"
}

call_bearer sessions-bearer-before "$bearer_token" GET /api/auth/sessions
check "Bearer-only GET /api/auth/sessions" 200
expect "Bearer-only: exactly one session is marked as current" "[.[] | select(.isCurrent)] | length == 1" "$EVIDENCE_DIR/sessions-bearer-before.json"

call_bearer revoke-others-bearer "$bearer_token" POST /api/auth/sessions/revoke-others
check "Bearer-only POST /api/auth/sessions/revoke-others" 204

call_bearer sessions-bearer-after "$bearer_token" GET /api/auth/sessions
check "Bearer-only GET /api/auth/sessions after revoking the others" 200
expect "Bearer-only: only the caller's session is left, and it is marked" "length == 1 and .[0].isCurrent" "$EVIDENCE_DIR/sessions-bearer-after.json"

echo "Evidence: $EVIDENCE_DIR"
if [ "$failures" -gt 0 ]; then
  echo "profile-sessions: $failures check(s) failed."
  exit 1
fi
echo "profile-sessions: all checks passed."
