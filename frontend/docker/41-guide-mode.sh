#!/bin/sh
# Runs at container start, after 40-write-runtime-config.sh. The image ships the private user guide
# (/etc/nginx/snippets/guide.conf = guide-private.conf); GUIDE_PUBLIC=true swaps in the public one.
# Anything other than true/false/empty is a typo, and a typo must not open the guide: it stays
# private, with a warning in the container log.
set -eu

snippets=/etc/nginx/snippets-available
target=/etc/nginx/snippets/guide.conf
mode=${GUIDE_PUBLIC:-}

case "$mode" in
  true)
    cp "$snippets/guide-public.conf" "$target"
    echo "41-guide-mode.sh: the user guide is public"
    ;;
  false | '')
    cp "$snippets/guide-private.conf" "$target"
    ;;
  *)
    cp "$snippets/guide-private.conf" "$target"
    echo "41-guide-mode.sh: GUIDE_PUBLIC=$mode is not true/false — serving the user guide privately" >&2
    ;;
esac
