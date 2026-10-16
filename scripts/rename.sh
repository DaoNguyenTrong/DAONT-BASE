#!/usr/bin/env bash
# Renames the project (C# namespaces, solution/project paths, DB/volume/package names) in one pass.
# Read RENAMING.md § Caveats first if the fork already runs somewhere with real data.
#
#   scripts/rename.sh NewName                     # rename StarterKit → NewName
#   scripts/rename.sh --dry-run NewName           # list the path moves and files, change nothing
#   scripts/rename.sh --from OldName NewName      # a fork renaming itself a second time
#   scripts/rename.sh --include-local NewName     # also rewrite gitignored appsettings*.json / .env*
#
# Every case form is derived from the PascalCase name: StarterKit → starterKit, starterkit,
# starter-kit, STARTERKIT. Prose ("starter kit") is left alone. History stays as written: plans/,
# CHANGELOG.md, .claude/decisions.md, RENAMING.md and this script are not rewritten.
set -euo pipefail

usage() {
  sed -n '2,12p' "$0" | sed 's/^# \{0,1\}//'
  exit "${1:-0}"
}

from=StarterKit
dry_run=false
include_local=false
to=""
while [ $# -gt 0 ]; do
  case "$1" in
    --from) from="${2:?--from needs a value}"; shift 2 ;;
    --dry-run) dry_run=true; shift ;;
    --include-local) include_local=true; shift ;;
    -h|--help) usage 0 ;;
    -*) echo "Unknown option: $1" >&2; usage 1 ;;
    *) [ -z "$to" ] || usage 1; to="$1"; shift ;;
  esac
done
[ -n "$to" ] || usage 1

pascal_re='^[A-Z][A-Za-z0-9]*$'
for name in "$from" "$to"; do
  [[ "$name" =~ $pascal_re ]] || { echo "✖ '$name' is not PascalCase (e.g. Acme, CimpVbi)" >&2; exit 1; }
done
[ "$from" != "$to" ] || { echo "✖ The new name is the current one" >&2; exit 1; }

lower() { printf '%s' "$1" | tr '[:upper:]' '[:lower:]'; }
upper() { printf '%s' "$1" | tr '[:lower:]' '[:upper:]'; }
camel() { printf '%s%s' "$(lower "${1:0:1}")" "${1:1}"; }
kebab() { lower "$(printf '%s' "$1" | sed -E 's/([a-z0-9])([A-Z])/\1-\2/g')"; }

export RN_FROM_PASCAL="$from" RN_TO_PASCAL="$to"
export RN_FROM_CAMEL="$(camel "$from")" RN_TO_CAMEL="$(camel "$to")"
export RN_FROM_LOWER="$(lower "$from")" RN_TO_LOWER="$(lower "$to")"
export RN_FROM_KEBAB="$(kebab "$from")" RN_TO_KEBAB="$(kebab "$to")"
export RN_FROM_UPPER="$(upper "$from")" RN_TO_UPPER="$(upper "$to")"

# A new name that contains the old one would still match after every move (the path loop below would
# never end) and could never pass the final check.
case "$RN_TO_LOWER" in
  *"$RN_FROM_LOWER"*)
    echo "✖ '$to' contains '$from' — rename to a temporary name first, then to '$to'." >&2
    exit 1 ;;
esac

# A one-word old name (Acme) has the same camel, lower and kebab form ("acme"), so the original form of
# each occurrence is lost. Such a collision becomes the lowercase new form — valid everywhere the
# others are (identifiers, database and package names), unlike kebab-case in an identifier.
if [ "$RN_FROM_LOWER" = "$RN_FROM_KEBAB" ] || [ "$RN_FROM_LOWER" = "$RN_FROM_CAMEL" ]; then
  echo "⚠ '$RN_FROM_LOWER' may stand for several case forms; every occurrence becomes '$RN_TO_LOWER'." >&2
fi

# perl, not sed -i: the same invocation works with GNU and BSD/macOS userlands. Longest form first,
# so the alternation never matches a shorter form inside a longer one. LOWER is listed last so it wins
# when two old forms are the same string (see above).
replace_program='
  BEGIN {
    %m = map { $ENV{"RN_FROM_$_"} => $ENV{"RN_TO_$_"} } qw(KEBAB CAMEL PASCAL UPPER LOWER);
    $re = join "|", map { quotemeta } sort { length $b <=> length $a } keys %m;
  }
  s/($re)/$m{$1}/g;
'
replace_stream() { perl -pe "$replace_program"; }
replace_files() { xargs -0 perl -pi -e "$replace_program"; }

cd "$(git rev-parse --show-toplevel)"

forms_re="$RN_FROM_PASCAL|$RN_FROM_CAMEL|$RN_FROM_LOWER|$RN_FROM_KEBAB|$RN_FROM_UPPER"
excludes=(
  ':(exclude)plans'
  ':(exclude)CHANGELOG.md'
  ':(exclude).claude/decisions.md'
  ':(exclude)RENAMING.md'
  ':(exclude)scripts/rename.sh'
)

if ! $dry_run && ! { git diff --quiet && git diff --cached --quiet; }; then
  echo "✖ Commit or stash tracked changes first — the rename should be one reviewable diff." >&2
  exit 1
fi

echo "▶ $RN_FROM_PASCAL → $RN_TO_PASCAL ($RN_TO_CAMEL, $RN_TO_LOWER, $RN_TO_KEBAB, $RN_TO_UPPER)"

# 1. Paths. Move the shallowest matching component first (a whole project folder in one `git mv`,
# which carries its gitignored bin/, obj/ and appsettings.json along), then look again.
echo "▶ Paths"
moved=0
while path="$(git ls-files -- . "${excludes[@]}" | grep -m1 -E "$forms_re" || true)"; [ -n "$path" ]; do
  prefix=""
  IFS='/' read -r -a parts <<<"$path"
  for part in "${parts[@]}"; do
    if printf '%s' "$part" | grep -qE "$forms_re"; then
      src="${prefix}${part}"
      dst="${prefix}$(printf '%s' "$part" | replace_stream)"
      break
    fi
    prefix="${prefix}${part}/"
  done
  echo "  $src → $dst"
  if $dry_run; then
    # Nothing moves in a dry run, so skip everything under this path instead of finding it again.
    excludes+=(":(exclude)$src")
  else
    git mv "$src" "$dst"
  fi
  moved=$((moved + 1))
done
echo "  ($moved moved)"
$dry_run && excludes=("${excludes[@]:0:5}")

# 2. Contents of tracked text files.
files=()
while IFS= read -r -d '' f; do files+=("$f"); done \
  < <(git grep -lzIE "$forms_re" -- . "${excludes[@]}" || true)
echo "▶ Contents: ${#files[@]} files"
if $dry_run; then
  printf '  %s\n' "${files[@]}"
elif [ ${#files[@]} -gt 0 ]; then
  printf '%s\0' "${files[@]}" | replace_files
fi

# 3. Gitignored local config — holds secrets, never committed, so every developer redoes this.
local_files=()
while IFS= read -r -d '' f; do
  grep -qE "$forms_re" "$f" && local_files+=("$f")
done < <(git ls-files -z --others --ignored --exclude-standard -- \
  ':(glob)**/appsettings*.json' ':(glob)**/.env' ':(glob)**/.env.*' \
  ':(exclude,glob)**/node_modules/**' ':(exclude,glob)**/bin/**' ':(exclude,glob)**/obj/**')
if [ ${#local_files[@]} -gt 0 ]; then
  if $include_local && ! $dry_run; then
    echo "▶ Local config rewritten:"
    printf '%s\0' "${local_files[@]}" | replace_files
    # A developer pulling the renamed commit gets the tracked files moved, not their gitignored
    # appsettings.json — move it into the renamed project folder when that exists.
    for i in "${!local_files[@]}"; do
      f="${local_files[$i]}"
      dst="$(printf '%s' "$f" | replace_stream)"
      [ "$dst" != "$f" ] && [ -d "$(dirname "$dst")" ] || continue
      if [ -e "$dst" ]; then
        local_files[$i]="$f (not moved — $dst already exists)"
      else
        mv "$f" "$dst"
        local_files[$i]="$f → $dst"
      fi
    done
  else
    echo "▶ Local config still naming $RN_FROM_PASCAL (gitignored — rerun with --include-local, or edit by hand):"
  fi
  printf '  %s\n' "${local_files[@]}"
fi

if $dry_run; then
  echo "✔ Dry run — nothing changed."
  exit 0
fi

# 4. Done only when no exact form is left outside the history files.
leftover="$(git grep -nIE "$forms_re" -- . "${excludes[@]}" || true)"
if [ -n "$leftover" ]; then
  echo "✖ Still naming $RN_FROM_PASCAL:" >&2
  printf '%s\n' "$leftover" >&2
  exit 1
fi

# Spaced/hyphenated variants ("starter kit") are prose and deliberately not rewritten.
prose_re="$(printf '%s' "$RN_FROM_KEBAB" | sed 's/-/[ _.-]?/g')"
echo "✔ Renamed. Prose still mentioning the old name — review by hand:"
git grep -nIiE "$prose_re" -- . "${excludes[@]}" | sed 's/^/  /' || true

cat <<EOF

Next:
  rm -rf backend/src/*/bin backend/src/*/obj backend/tests/*/bin backend/tests/*/obj
  dotnet restore backend/$RN_TO_PASCAL.sln && backend/scripts/test.sh
  bun install && bun install --cwd frontend && bun run --cwd frontend test:run && bun run --cwd frontend build

Runtime identifiers changed — read RENAMING.md § Caveats before deploying over existing data:
  database ${RN_TO_PASCAL}_api, Docker volumes $RN_TO_LOWER-*, Prometheus job $RN_TO_LOWER-api,
  JWT Issuer/Audiences, DataProtection purpose "$RN_TO_PASCAL.Secrets", storage bucket $RN_TO_LOWER.
EOF
