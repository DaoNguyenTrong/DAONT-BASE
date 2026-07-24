# API Contract (backend ↔ frontend)

Read this when changing a backend endpoint, request/response DTO, or header contract that the frontend consumes.

## Current State (as of the monorepo restructure)

`shared/openapi/` is a **placeholder** — reserved for the backend's exported OpenAPI spec, not yet wired up (see `shared/openapi/README.md` and `.claude/decisions.md`). There is no codegen pipeline today. Until it exists:

- The frontend's request/response types (`frontend/src/api/types.ts`) are **hand-maintained** — they are not generated from `backend/src/StarterKit.API`'s actual contract.
- Nothing automatically fails or warns when a backend DTO and its frontend counterpart drift apart. Treat this as a manual, easy-to-miss sync step.

## Rule: Changing a Backend Endpoint or DTO

Whenever you change a controller route, request/response DTO shape, status code, or error contract in `backend/src/StarterKit.API` or `backend/src/StarterKit.Application`:

1. Check whether `frontend/src/api/{resource}-api.ts` calls that endpoint and whether `frontend/src/api/types.ts` has a matching type.
2. Update the frontend type/call to match — same field names/casing (backend DTOs are typically `PascalCase` in C# but serialize `camelCase` over the wire; frontend types should match the wire shape, not the C# property names).
3. If the change affects error responses, remember the frontend maps errors through `ApiError`/`ProblemDetails` (`frontend/src/api/client.ts`) — check `toProblemDetails`/`toApiError` still handle the shape.
4. If the change affects headers (`X-Tenant-Id`, `X-TimeZone`, `Authorization`/`access_token` cookie — see `authentication.md`), check `frontend/src/api/client.ts` still sends them correctly.

## When Codegen Gets Wired Up

The intended end-state (per `.claude/decisions.md`): the backend exports its OpenAPI spec into `shared/openapi/`, and the frontend generates its client/types from that spec instead of hand-maintaining `frontend/src/api/types.ts`. When that pipeline exists, this file should be updated to document the generation command and to retire the manual hand-sync rule above.
