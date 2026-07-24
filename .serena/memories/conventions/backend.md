# Backend C# Conventions

## Naming
- PascalCase: classes, methods, properties, public fields
- camelCase: local variables, parameters, primary-constructor-derived private fields
- _camelCase: private fields in traditional-constructor classes
- Async methods: suffix with `Async`, always pass `CancellationToken ct`

## Variable Declarations
Avoid `var` — use explicit types:
```csharp
Account? account = await repository.GetByIdAsync(id, ct);
List<string> items = new();
```

## Entity Pattern (CRITICAL)
Private constructor + `static Create(XxxParams)` factory + `Update(XxxParams)`. Domain validation in `Update`. Never construct via EF or Mapperly directly.
```csharp
public record ProjectParams(string Name, long MonthlyTokenLimit);
public sealed class Project : BaseEntity {
    private Project() { }
    public static Project Create(ProjectParams p) { Project e = new(); e.Update(p); return e; }
    public void Update(ProjectParams p) { /* validate then assign */ }
}
```

## Mapperly
- `EntityMapper.ToDto(entity)` → DTO
- `request.ToParams()` → Params record (extension method)

## EF Core (NoTracking global)
```csharp
// Update — must call repository.Update() to re-attach
entity.Update(request.ToParams());
repository.Update(entity);
await unitOfWork.SaveChangesAsync(ct);
```

## Null Handling
Prefer `?.` / `??` / `?? throw` — avoid `!` operator.

## Layer Rules
API → Application → Domain. Infrastructure implements Domain interfaces. No cross-layer leaks.
