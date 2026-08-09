---
paths:
  - "backend/**"
---

# Architecture

Scope: `backend/` only. For `frontend/` structure and conventions, see `frontend-conventions.md`; for the cross-cutting API contract between the two, see `api-contract.md`.

Clean Architecture — four layers:

| Layer          | Project                   | Responsibility                                                          |
|----------------|---------------------------|-------------------------------------------------------------------------|
| Domain         | `StarterKit.Domain`         | Entities, interfaces (`IRepository<T,TId>`), domain exceptions          |
| Application    | `StarterKit.Application`    | Service interfaces & implementations, DTOs, settings, Mapperly mappings |
| Infrastructure | `StarterKit.Infrastructure` | EF Core (PostgreSQL), repositories, JWT, AI services                    |
| API            | `StarterKit.API`            | Controllers, middleware, OpenAPI/Scalar, `Program.cs`                   |

Dependencies flow inward: API → Application → Domain. Infrastructure implements Domain interfaces.

All four layer projects live under `backend/src/`; tests mirror the split under `backend/tests/`.
