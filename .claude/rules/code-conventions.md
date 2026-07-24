# C# Code Conventions

## Variable Declarations

Avoid `var` — use explicit types.

```csharp
Account? account = await repository.GetByIdAsync(id, ct);
List<string> items = new();
IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
```

## Naming

- **PascalCase**: classes, methods, properties, public fields
- **camelCase**: local variables, parameters; also primary-constructor-derived private fields
- **\_camelCase**: private fields in traditional-constructor classes

```csharp
// Primary constructor — parameters captured directly, extra fields are camelCase
public sealed class AuthService(IOptions<JwtSettings> jwtOptions) : IAuthService
{
    private readonly JwtSettings jwtSettings = jwtOptions.Value; // camelCase
}

// Traditional constructor — fields are _camelCase
public sealed class SemanticKernelLlmService : ILlmService
{
    private readonly ILogger<SemanticKernelLlmService> _logger;
    public SemanticKernelLlmService(ILogger<SemanticKernelLlmService> logger) => _logger = logger;
}
```

## Async/Await

- Suffix async methods with `Async`
- Always pass `CancellationToken ct` through the call chain

## Null Handling

- Use nullable reference types (`string?`, `Account?`)
- Prefer `?.` / `??` / `?? throw` — avoid the `!` operator

## Entity Pattern

Entities use **private constructor + `Create(XxxParams)` factory + `Update(XxxParams)`**. All domain validation lives in `Update`.

```csharp
public record ProjectParams(string Name, long MonthlyTokenLimit);

public sealed class Project : BaseEntity
{
    private Project() { }
    public static Project Create(ProjectParams p) { Project e = new(); e.Update(p); return e; }
    public void Update(ProjectParams p) { /* validate then assign */ }
}

// In service
Project project = Project.Create(request.ToParams());  // ToParams() = Mapperly extension
```

Never construct entities via EF or Mapperly directly — that bypasses domain validation.

## Mapperly

- `EntityMapper.ToDto(entity)` — entity → DTO
- `request.ToParams()` — request → Params record (extension method in `EntityMapper`)

## EF Core (NoTracking global)

```csharp
// Create
await repository.AddAsync(entity, ct);
await unitOfWork.SaveChangesAsync(ct);

// Update — repository.Update() required to re-attach detached entity
Project project = await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException(...);
project.Update(request.ToParams());
repository.Update(project);   // marks ALL columns modified
await unitOfWork.SaveChangesAsync(ct);

// Delete
repository.Delete(project);   // handles detached automatically
await unitOfWork.SaveChangesAsync(ct);
```
