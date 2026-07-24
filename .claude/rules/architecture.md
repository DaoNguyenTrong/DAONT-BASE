# Architecture

Scope: `backend/` only. For `frontend/` structure and conventions, see `frontend-conventions.md`; for the cross-cutting API contract between the two, see `api-contract.md`.

Clean Architecture — four layers:

| Layer          | Project                   | Responsibility                                                          |
|----------------|---------------------------|-------------------------------------------------------------------------|
| Domain         | `FeedbackHub.Domain`         | Entities, interfaces (`IRepository<T,TId>`), domain exceptions          |
| Application    | `FeedbackHub.Application`    | Service interfaces & implementations, DTOs, settings, Mapperly mappings |
| Infrastructure | `FeedbackHub.Infrastructure` | EF Core (PostgreSQL), repositories, JWT, AI services                    |
| API            | `FeedbackHub.API`            | Controllers, middleware, OpenAPI/Scalar, `Program.cs`                   |

Dependencies flow inward: API → Application → Domain. Infrastructure implements Domain interfaces.

All four layer projects live under `backend/src/`; tests mirror the split under `backend/tests/`.
