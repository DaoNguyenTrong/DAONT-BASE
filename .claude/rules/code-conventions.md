---
paths:
  - "backend/**"
---

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

One `[Mapper] static partial class` per feature, next to its service (`Services/Accounts/AccountMapper.cs`, `Services/Notifications/NotificationMapper.cs`) — no shared mapper in `Common/`, which would make every feature namespace depend on every other one (`ArchitectureTests.Namespaces_HaveNoDependencyCycles` fails on that).

- `AccountMapper.ToDto(entity)` — entity → DTO
- `request.ToParams()` — request → Params record (extension method in the feature's mapper)

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

## Hangfire Jobs

- **One lock across every entry point of a job.** `[DisableConcurrentExecution]` without a `resource` locks on `Type.Method` (Hangfire 1.8), so a recurring `RunAsync` and an on-startup `RunIfMissingAsync` of the same job only exclude *themselves* and can run the same work at once. Give every entry point the same explicit resource (`[DisableConcurrentExecution(RecurringJobId, timeoutInSeconds)]`), and test that the attributes' `Resource` values are equal and non-null — not just that the attribute exists.
- **Every queue you enqueue to must be in `options.Queues`** (`HangfireExtensions`). A job enqueued on an unlisted queue sits in storage forever with no error; add the queue there in the same change that first uses it (see `NotificationQueues.Default`).
- **Jobs and seeders are idempotent.** Hangfire retries a failed job (`[AutomaticRetry]`) and a startup seeder runs on every boot and on every instance, so re-running either must leave the same state — check-then-insert or upsert, never a blind insert.
