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
| Infrastructure | `StarterKit.Infrastructure` | EF Core (PostgreSQL), repositories, JWT/API-key auth, caching, storage, email, Hangfire jobs |
| API            | `StarterKit.API`            | Controllers, middleware, OpenAPI/Scalar, `Program.cs`                   |

Dependencies flow inward: API → Application → Domain. Infrastructure implements Domain interfaces.

All four layer projects live under `backend/src/`; tests mirror the split under `backend/tests/`.

`backend/tests/StarterKit.API.Tests/Architecture/ArchitectureTests.cs` enforces what `ProjectReference`s can't: no infrastructure/web packages in Domain/Application, Infrastructure used in the API only from `Program.cs`, the entity factory pattern, `AppException`-derived exceptions, and **no namespace dependency cycles** inside a project. A type shared between two features lives with the side that is depended on (a port next to its consumer, a mapper next to its feature) — never in a `Common/` namespace that would point back at a feature.
