#!/bin/sh
# Runs at container start (the nginx image executes /docker-entrypoint.d/*.sh before nginx).
# Writes the SPA's /config.json from the container's VITE_* environment, so one built image can
# enable OAuth/push or point at another backend per environment without a rebuild — see
# frontend/src/lib/runtime-config.ts. Empty/unset variables are left out, and the app then falls
# back to the value baked in at build time for that key. Use EITHER these variables OR a bind-mounted
# config.json: if none is set, or the existing file isn't writable (read-only mount), it is left
# untouched.
set -eu

target=/usr/share/nginx/html/config.json
nl='
'
entries=''

# Control characters (a CR from a Windows-edited .env, a stray tab) are dropped rather than escaped:
# these values are identifiers, and a raw one would make the whole file invalid JSON — which the app
# then ignores entirely, silently disabling every override.
json_escape() {
  printf '%s' "$1" | tr -d '\000-\037' | sed -e 's/\\/\\\\/g' -e 's/"/\\"/g'
}

add() {
  value=$(printenv "$2" || true)
  [ -n "$value" ] || return 0
  entries="${entries}${entries:+,}${nl}  \"$1\": \"$(json_escape "$value")\""
}

add apiBaseUrl VITE_API_BASE_URL
add googleClientId VITE_GOOGLE_CLIENT_ID
add microsoftClientId VITE_MICROSOFT_CLIENT_ID
add microsoftTenantId VITE_MICROSOFT_TENANT_ID
add firebaseApiKey VITE_FIREBASE_API_KEY
add firebaseAuthDomain VITE_FIREBASE_AUTH_DOMAIN
add firebaseProjectId VITE_FIREBASE_PROJECT_ID
add firebaseMessagingSenderId VITE_FIREBASE_MESSAGING_SENDER_ID
add firebaseAppId VITE_FIREBASE_APP_ID
add firebaseVapidKey VITE_FIREBASE_VAPID_KEY

if [ -z "$entries" ]; then
  echo "$0: no VITE_* runtime values set, leaving config.json as is"
  exit 0
fi

if [ -e "$target" ] && [ ! -w "$target" ]; then
  echo "$0: $target is not writable (read-only mount?) — keeping it, VITE_* env ignored" >&2
  exit 0
fi

printf '{%s\n}\n' "$entries" > "$target"
echo "$0: wrote $target"
