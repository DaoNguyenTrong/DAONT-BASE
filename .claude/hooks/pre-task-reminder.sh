#!/usr/bin/env bash
set -euo pipefail

REMINDER=$(cat <<'EOF'
[Pre-Task Protocol] For any non-trivial task, follow this order (see CLAUDE.md):
Memory (.claude/decisions.md) -> Live state (git status/diff) -> Orient macro (GitNexus: gitnexus_query/gitnexus_context) -> Orient micro (Serena: find_symbol/find_referencing_symbols) -> Impact check (gitnexus_impact on every symbol before editing; flag HIGH/CRITICAL risk) -> Execute -> Verify (gitnexus_detect_changes, then run the test suite for whichever side changed: dotnet test backend/FEEDBACK-HUB.sln --no-restore -m:1 for backend/, bun run --cwd frontend test:run for frontend/ - must pass before commit) -> Log decisions (.claude/decisions.md, expensive/non-obvious ones only - skip routine decisions).
Never grep or open a file to orient first - GitNexus, then Serena, then Read.
EOF
)

jq -n --arg text "$REMINDER" '{hookSpecificOutput: {hookEventName: "UserPromptSubmit", additionalContext: $text}}'
