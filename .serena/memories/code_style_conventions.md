# Code Style & Conventions

- **Avoid `var`** — use explicit types everywhere.
- **Naming**: PascalCase for classes/methods/properties/public fields; camelCase for locals/parameters and primary-constructor-derived private fields; `_camelCase` for private fields in traditional-constructor classes.
- **Async**: suffix async methods with `Async`; always thread `CancellationToken ct` through call chains.
- **Nullability**: use `string?`/`Account?` etc.; prefer `?.` / `??` / `?? throw`; avoid the `!` null-forgiving operator.
- **Entity Pattern (critical invariant)**: private constructor + `static Create(XxxParams p)` factory + `Update(XxxParams p)`. ALL domain validation lives in `Update`. Never construct entities via EF or Mapperly directly — always use the factory, or domain validation is bypassed.
- **Mapperly**: `EntityMapper.ToDto(entity)` for entity→DTO; `request.ToParams()` extension methods for request→Params record, defined in `EntityMapper`.
- **EF Core**: global `NoTracking` query behavior. Update flow requires `repository.Update(entity)` to re-attach a detached entity (marks all columns modified).
- **No comments** unless explaining a non-obvious WHY (hidden constraint, subtle invariant, workaround). Never explain WHAT the code does.
