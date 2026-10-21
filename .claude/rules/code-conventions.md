---
paths:
  - "backend/**"
---

# C# Code Conventions

## Variable Declarations

Avoid `var` — use explicit types. Enforced: the root `.editorconfig` sets IDE0008 to `error` and `EnforceCodeStyleInBuild` (`backend/Directory.Build.props`) fails the build on it; `dotnet format style backend/StarterKit.sln --diagnostics IDE0008 --severity error --no-restore` fixes most hits.

```csharp
Account? account = await repository.GetByIdAsync(id, ct);
List<string> items = new();
IRepository<Account, Guid> repository = unitOfWork.Repository<Account, Guid>();
```

## Naming

- **PascalCase**: classes, methods, properties, public fields
- **camelCase**: local variables, parameters; also primary-constructor-derived private fields
- **\_camelCase**: private fields in traditional-constructor classes

Services use primary constructors (no traditional-constructor service exists in `backend/src`), so their private
fields are camelCase:

```csharp
public sealed class AuthController(IOptions<JwtSettings> jwtOptions, ...) : ControllerBase
{
    private readonly JwtSettings jwtSettings = jwtOptions.Value; // camelCase, not _jwtSettings
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
public record OrganizationParams(string Name, string Slug, bool Status = true);

public sealed class Organization : BaseEntity
{
    private Organization() { }
    public static Organization Create(OrganizationParams p) { Organization e = new(); e.Update(p); return e; }
    public void Update(OrganizationParams p) { /* validate then assign */ }
}

// In service
Organization organization = Organization.Create(request.ToParams());  // ToParams() = Mapperly extension
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
Organization organization = await repository.GetByIdAsync(id, ct) ?? throw new NotFoundException(...);
organization.Update(request.ToParams());
repository.Update(organization);   // marks ALL columns modified
await unitOfWork.SaveChangesAsync(ct);

// Delete
repository.Delete(organization);   // handles detached automatically
await unitOfWork.SaveChangesAsync(ct);
```

## Hangfire Jobs

- **One lock across every entry point of a job.** `[DisableConcurrentExecution]` without a `resource` locks on `Type.Method` (Hangfire 1.8), so a recurring `RunAsync` and an on-startup `RunIfMissingAsync` of the same job only exclude *themselves* and can run the same work at once. Give every entry point the same explicit resource (`[DisableConcurrentExecution(RecurringJobId, timeoutInSeconds)]`), and test that the attributes' `Resource` values are equal and non-null — not just that the attribute exists.
- **Every queue you enqueue to must be in `options.Queues`** (`HangfireExtensions`). A job enqueued on an unlisted queue sits in storage forever with no error; add the queue there in the same change that first uses it (see `NotificationQueues.Default`).
- **Jobs and seeders are idempotent.** Hangfire retries a failed job (`[AutomaticRetry]`) and a startup seeder runs on every boot and on every instance, so re-running either must leave the same state — check-then-insert or upsert, never a blind insert.
