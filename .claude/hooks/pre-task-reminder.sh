#!/usr/bin/env bash
set -euo pipefail

REMINDER=$(cat <<'EOF'
[Pre-Task Protocol] For any non-trivial task, follow this order (see CLAUDE.md):
Memory (.claude/decisions.md) -> Live state (git status/diff) -> Orient macro (CodeGraph: codegraph_explore) -> Orient micro (Serena: find_symbol/find_referencing_symbols, backend C# only) -> Impact check (codegraph_explore on every symbol before editing; review the returned blast-radius/callers) -> Execute -> Verify (run the test suite for whichever side changed: dotnet test backend/StarterKit.sln --no-restore -m:1 for backend/, bun run --cwd frontend test:run for frontend/ - must pass before commit) -> Log decisions (.claude/decisions.md, system-design ones only - architecture/contracts/security, skip branding/UI/copy/routine bug fixes; title format '### YYYY-MM-DD - <decision>'; GATE: draft the entry and get explicit user confirmation before writing, never write unprompted).
Never grep or open a file to orient first - CodeGraph, then Serena, then Read.
EOF
)

jq -n --arg text "$REMINDER" '{hookSpecificOutput: {hookEventName: "UserPromptSubmit", additionalContext: $text}}'
