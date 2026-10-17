#!/usr/bin/env bash
# Integration check for the user guide's access modes (frontend/docker/guide-*.conf). Builds the web
# image, runs it against a stub `api` that accepts the cookie `access_token=good`, and checks what nginx
# answers with curl. Local only — CI doesn't build images. Needs Docker; run from anywhere in the repo:
#
#   frontend/docker/verify-guide-access.sh            # build the image, then check
#   SKIP_BUILD=1 frontend/docker/verify-guide-access.sh   # reuse the last build
set -euo pipefail

cd "$(git rev-parse --show-toplevel)"

image=${IMAGE:-starterkit-web:guide-check}
run_id="guide-check-$$"
network="$run_id"
api="$run_id-api"
failures=0

cleanup() {
  docker rm -f "$api" "$run_id-private" "$run_id-public" "$run_id-invalid" >/dev/null 2>&1 || true
  docker network rm "$network" >/dev/null 2>&1 || true
}
trap cleanup EXIT

if [ "${SKIP_BUILD:-}" != 1 ]; then
  echo "Building $image ..."
  docker build -q -f frontend/Dockerfile -t "$image" . >/dev/null
fi

docker network create "$network" >/dev/null

# The stub answers only what nginx's auth_request asks; nginx resolves `api` at start, so it must be up first.
docker run -d --name "$api" --network "$network" --network-alias api nginx:alpine sh -c '
cat > /etc/nginx/conf.d/default.conf <<EOF
server {
  listen 8080;
  location = /api/auth/check {
    if (\$http_cookie ~ "access_token=good") { return 204; }
    # Accepts any API key, so a 302 below proves nginx strips the header before asking.
    if (\$http_x_api_key != "") { return 204; }
    return 401;
  }
  location / { return 404; }
}
EOF
exec nginx -g "daemon off;"' >/dev/null

# start <name> [GUIDE_PUBLIC value] — prints the host port
start() {
  local name="$run_id-$1"
  local env=()
  [ $# -ge 2 ] && env=(-e "GUIDE_PUBLIC=$2")
  docker run -d --name "$name" --network "$network" -p 127.0.0.1::80 "${env[@]}" "$image" >/dev/null
  local port
  port=$(docker port "$name" 80/tcp | head -n1 | sed 's/.*://')
  for _ in $(seq 1 50); do
    curl -fs -o /dev/null "http://127.0.0.1:$port/" && break
    sleep 0.2
  done
  echo "$port"
}

check() {
  local name=$1 expected=$2 actual=$3
  if [ "$actual" = "$expected" ]; then
    echo "PASS  $name"
  else
    echo "FAIL  $name"
    echo "        expected: $expected"
    echo "        actual:   ${actual:0:200}"
    failures=$((failures + 1))
  fi
}

# contains <name> <needle> <haystack> [not]
contains() {
  local name=$1 needle=$2 haystack=$3 negate=${4:-}
  if grep -qF -- "$needle" <<<"$haystack"; then found=yes; else found=no; fi
  if { [ -z "$negate" ] && [ $found = yes ]; } || { [ -n "$negate" ] && [ $found = no ]; }; then
    echo "PASS  $name"
  else
    echo "FAIL  $name (${negate:+un}expected \"$needle\")"
    failures=$((failures + 1))
  fi
}

code() { curl -s -o /dev/null -w '%{http_code}' "$@"; }
header() { curl -s -o /dev/null -D - "${@:2}" | tr -d '\r' | sed -n "s/^$1: //Ip"; }

private=$(start private)
public=$(start public true)
invalid=$(start invalid abc)
p="http://127.0.0.1:$private"

check "private: no session -> 302" 302 "$(code "$p/guide/faq")"
check "private: no session -> guide-auth with next" "/#/guide-auth?next=/guide/faq" "$(header location "$p/guide/faq")"
check "private: session -> 200" 200 "$(code -H 'Cookie: access_token=good' "$p/guide/faq")"
contains "private: session -> page content" "Câu hỏi thường gặp" "$(curl -s -H 'Cookie: access_token=good' "$p/guide/faq")"
check "private: session -> assets cached privately" "private, max-age=31536000, immutable" \
  "$(header cache-control -H 'Cookie: access_token=good' "$p$(curl -s -H 'Cookie: access_token=good' "$p/guide/faq" | grep -o '/guide/assets/[^"]*\.js' | head -n1)")"
check "private: no session on an asset -> 302" 302 "$(code "$p$(curl -s -H 'Cookie: access_token=good' "$p/guide/faq" | grep -o '/guide/assets/[^"]*\.js' | head -n1)")"
check "private: unknown page with session -> 404" 404 "$(code -H 'Cookie: access_token=good' "$p/guide/nope")"
check "private: /guide -> 301" 301 "$(code "$p/guide")"
check "private: /guide -> /guide/" "/guide/" "$(header location "$p/guide")"
check "private: _mode" private "$(curl -s "$p/guide/_mode")"
check "private: query dropped from next" "/#/guide-auth?next=/guide/faq" "$(header location "$p/guide/faq?x=1&y=2")"
crlf=$(curl -s -o /dev/null -D - "$p/guide/%0d%0aSet-Cookie:x" | tr -d '\r')
contains "private: encoded CRLF -> 302" "302" "$(head -n1 <<<"$crlf")"
check "private: encoded CRLF stays encoded in Location" "/#/guide-auth?next=/guide/%0d%0aSet-Cookie:x" \
  "$(sed -n 's/^location: //Ip' <<<"$crlf")"
check "private: encoded CRLF adds no header line" "" "$(grep -i '^set-cookie' <<<"$crlf" || true)"
check "private: app still served" 200 "$(code "$p/")"
contains "private: app shell" '<div id="app"' "$(curl -s "$p/")"
check "private: X-Api-Key is stripped before the auth request -> 302" 302 "$(code -H 'X-Api-Key: good' "$p/guide/faq")"

u="http://127.0.0.1:$public"
check "public: no session -> 200" 200 "$(code "$u/guide/faq")"
check "public: _mode" public "$(curl -s "$u/guide/_mode")"
check "public: unknown page -> 404" 404 "$(code "$u/guide/nope")"

i="http://127.0.0.1:$invalid"
check "invalid GUIDE_PUBLIC: stays private" private "$(curl -s "$i/guide/_mode")"
check "invalid GUIDE_PUBLIC: no session -> 302" 302 "$(code "$i/guide/faq")"
contains "invalid GUIDE_PUBLIC: warning logged" "GUIDE_PUBLIC=abc" "$(docker logs "$run_id-invalid" 2>&1)"

# A hung API (accepts the connection, never answers) must not hold every guide request for a minute.
docker pause "$api" >/dev/null
check "private: API hung -> error page within 15s" 500 \
  "$(code --max-time 15 -H 'Cookie: access_token=good' "$p/guide/faq")"
docker unpause "$api" >/dev/null

# Last: the API goes away. The guide must fail closed (an error page), never serve content.
docker stop "$api" >/dev/null
check "private: API down -> 500" 500 "$(code -H 'Cookie: access_token=good' "$p/guide/faq")"
down=$(curl -s -H 'Cookie: access_token=good' "$p/guide/faq")
contains "private: API down -> error page" "guide-unavailable" "$down"
contains "private: API down -> no content" "Câu hỏi thường gặp" "$down" not

echo
if [ $failures -eq 0 ]; then
  echo "All checks passed."
else
  echo "$failures check(s) failed."
  exit 1
fi
