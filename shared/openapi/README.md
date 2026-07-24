# OpenAPI Contract

`openapi.json` is the backend's exported OpenAPI spec (generated from `backend/src/StarterKit.API`), committed as the single source of truth for the API contract. The frontend generates its client/types from it with `orval` (`frontend/orval.config.ts` → `frontend/src/api/generated/**`).

Regenerate after any backend contract change — see `.claude/rules/api-contract.md` for the commands and how the generated client is wired into the frontend.
