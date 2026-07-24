---
name: crud-entity
description: Scaffold a new entity with full CRUD operations and pagination in Clean Architecture. Use when adding a new entity, creating CRUD endpoints, or building a new feature that needs database persistence.
disable-model-invocation: true
---

# CRUD Entity Scaffolding

Create a new entity with full CRUD + pagination following Clean Architecture.

## Workflow

```
Task Progress:
- [ ] 1. Create Entity (Domain layer)
- [ ] 2. Create EF Configuration (Infrastructure)
- [ ] 3. Register DbSet in AppDbContext
- [ ] 4. Create DTOs (Application)
- [ ] 5. Create Service interface + implementation
- [ ] 6. Add mapping to EntityMapper
- [ ] 7. Register DI
- [ ] 8. Create Controller (API)
- [ ] 9. Create Migration
- [ ] 10. Build & Test
```

---

## Step 1: Entity (Domain)

File: `backend/src/StarterKit.Domain/Entities/{EntityName}.cs`

```csharp
using StarterKit.Domain.Exceptions;

namespace StarterKit.Domain.Entities;

// Params record - dùng cho cả Create và Update
public record {EntityName}Params(
    string Name,
    string? Description = null);

public sealed class {EntityName} : BaseEntity
{
    private {EntityName}() { } // EF Core

    // Factory method
    public static {EntityName} Create({EntityName}Params p)
    {
        {EntityName} entity = new();
        entity.Update(p);
        return entity;
    }

    // Properties - private set
    public string Name { get; private set; } = string.Empty;
    public string? Description { get; private set; }

    // Update method với validation
    public void Update({EntityName}Params p)
    {
        if (string.IsNullOrWhiteSpace(p.Name))
        {
            throw new DomainException("{EntityName} name is required.");
        }

        Name = p.Name.Trim();
        Description = string.IsNullOrWhiteSpace(p.Description) ? null : p.Description.Trim();
    }
}
```

**Rules:**
- Inherit from `BaseEntity` (Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy)
- **{EntityName}Params record** - group all params, dùng cho cả Create và Update
- **Factory method** `Create(params)` - không dùng public constructor
- Private parameterless constructor for EF Core
- Private setters, Update() method nhận Params record
- Domain validation trong Update() method

---

## Step 2: EF Configuration (Infrastructure)

File: `backend/src/StarterKit.Infrastructure/Persistence/Configurations/{EntityName}Configuration.cs`

```csharp
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class {EntityName}Configuration : IEntityTypeConfiguration<{EntityName}>
{
    public void Configure(EntityTypeBuilder<{EntityName}> builder)
    {
        builder.ToTable("{table_name}"); // lowercase, plural

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .HasMaxLength(200)
            .IsRequired();

        // Unique constraints
        builder.HasIndex(x => x.Email).IsUnique();

        // Relationships
        // builder.HasOne<Role>().WithMany().HasForeignKey(x => x.RoleId);
    }
}
```

---

## Step 3: DbContext

File: `backend/src/StarterKit.Infrastructure/Persistence/AppDbContext.cs`

Add DbSet (expression-bodied, following existing pattern):
```csharp
public DbSet<{EntityName}> {EntityName}s => Set<{EntityName}>();
```

---

## Step 4: DTOs (Application)

Folder: `backend/src/StarterKit.Application/Services/{EntityName}s/`

**{EntityName}Dto.cs:**
```csharp
namespace StarterKit.Application.Services.{EntityName}s;

public sealed record {EntityName}Dto(
    int Id,
    string Name,
    string? Description,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
```

**Create{EntityName}Request.cs:**
```csharp
using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.{EntityName}s;

public sealed record Create{EntityName}Request(
    [Required, MaxLength(200)] string Name,
    [MaxLength(500)] string? Description);
```

**Update{EntityName}Request.cs:**
```csharp
using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.{EntityName}s;

public sealed record Update{EntityName}Request(
    [Required, MaxLength(200)] string Name,
    [MaxLength(500)] string? Description);
```

**Notes:**
- DTO fields phải match với Entity properties + {EntityName}Params
- Dùng `int Id` (BaseEntity default), đổi sang `Guid` nếu cần
- Data Annotations cho validation ở API layer

---

## Step 5: Service

**I{EntityName}Service.cs:**
```csharp
using StarterKit.Application.Common.Models;

namespace StarterKit.Application.Services.{EntityName}s;

public interface I{EntityName}Service
{
    Task<PagedResult<{EntityName}Dto>> GetAllAsync(PaginationRequest request, CancellationToken ct = default);

    Task<{EntityName}Dto> GetByIdAsync(int id, CancellationToken ct = default);

    Task<{EntityName}Dto> CreateAsync(Create{EntityName}Request request, CancellationToken ct = default);

    Task<{EntityName}Dto> UpdateAsync(int id, Update{EntityName}Request request, CancellationToken ct = default);

    Task DeleteAsync(int id, CancellationToken ct = default);
}
```

**Notes:**
- Dùng `int` cho Id (BaseEntity default), đổi sang `Guid` nếu dùng `BaseEntity<Guid>`
- Không return nullable - throw `NotFoundException` khi không tìm thấy

**{EntityName}Service.cs:**
```csharp
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Mappings;
using StarterKit.Application.Common.Models;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Services.{EntityName}s;

public sealed class {EntityName}Service(IUnitOfWork unitOfWork) : I{EntityName}Service
{
    private const int DefaultPageNumber = 1;
    private const int DefaultPageSize = 10;

    public async Task<PagedResult<{EntityName}Dto>> GetAllAsync(PaginationRequest request, CancellationToken ct = default)
    {
        int pageNumber = request.PageNumber < 1 ? DefaultPageNumber : request.PageNumber;
        int pageSize = request.PageSize < 1 ? DefaultPageSize : request.PageSize;

        (IReadOnlyList<{EntityName}> items, int totalCount) = await unitOfWork.Repository<{EntityName}>()
            .ListPagedAsync(pageNumber, pageSize, ct);

        return new PagedResult<{EntityName}Dto>(
            items.Select(EntityMapper.ToDto).ToList(),
            totalCount, pageNumber, pageSize);
    }

    public async Task<{EntityName}Dto> GetByIdAsync(int id, CancellationToken ct = default)
    {
        {EntityName} entity = await unitOfWork.Repository<{EntityName}>().GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof({EntityName}), id);

        return EntityMapper.ToDto(entity);
    }

    public async Task<{EntityName}Dto> CreateAsync(Create{EntityName}Request request, CancellationToken ct = default)
    {
        {EntityName} entity = {EntityName}.Create(request.ToParams());

        await unitOfWork.Repository<{EntityName}>().AddAsync(entity, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return EntityMapper.ToDto(entity);
    }

    public async Task<{EntityName}Dto> UpdateAsync(int id, Update{EntityName}Request request, CancellationToken ct = default)
    {
        IRepository<{EntityName}> repository = unitOfWork.Repository<{EntityName}>();
        {EntityName} entity = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof({EntityName}), id);

        entity.Update(request.ToParams());

        repository.Update(entity);
        await unitOfWork.SaveChangesAsync(ct);

        return EntityMapper.ToDto(entity);
    }

    public async Task DeleteAsync(int id, CancellationToken ct = default)
    {
        IRepository<{EntityName}> repository = unitOfWork.Repository<{EntityName}>();
        {EntityName} entity = await repository.GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof({EntityName}), id);

        repository.Delete(entity);
        await unitOfWork.SaveChangesAsync(ct);
    }
}
```

**Key patterns:**
- Explicit types thay vì `var`
- `request.ToParams()` - Mapperly extension method
- `{EntityName}.Create()` - Factory method
- Throw `NotFoundException` thay vì return null
- Repository queries are `NoTracking` by default; call `repository.Update(entity)` after mutating an entity loaded with `GetByIdAsync`

---

## Step 6: Mapping

File: `backend/src/StarterKit.Application/Common/Mappings/EntityMapper.cs`

Add:
```csharp
// Entity → DTO
[MapperIgnoreSource(nameof({EntityName}.CreatedBy))]
[MapperIgnoreSource(nameof({EntityName}.UpdatedBy))]
public static partial {EntityName}Dto ToDto({EntityName} entity);

// Request → Params (extension methods for clean service code)
public static partial {EntityName}Params ToParams(this Create{EntityName}Request request);

public static partial {EntityName}Params ToParams(this Update{EntityName}Request request);
```

**Mapperly auto-generates:**
- `ToDto()` - Entity to DTO mapping
- `ToParams()` - Request to Params mapping (extension methods)

---

## Step 7: DI Registration

File: `backend/src/StarterKit.Application/DependencyInjection.cs`

Add:
```csharp
services.AddScoped<I{EntityName}Service, {EntityName}Service>();
```

---

## Step 8: Controller (API)

File: `backend/src/StarterKit.API/Controllers/{EntityName}sController.cs`

```csharp
using Microsoft.AspNetCore.Mvc;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.{EntityName}s;

namespace StarterKit.API.Controllers;

[ApiController]
[Route("api/[controller]")]
public sealed class {EntityName}sController(I{EntityName}Service service) : ControllerBase
{
    [HttpGet]
    public async Task<ActionResult<PagedResult<{EntityName}Dto>>> GetAll(
        [FromQuery] PaginationRequest request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetAllAsync(request, cancellationToken));
    }

    [HttpGet("{id:int}")]
    public async Task<ActionResult<{EntityName}Dto>> GetById(
        int id,
        CancellationToken cancellationToken)
    {
        return Ok(await service.GetByIdAsync(id, cancellationToken));
    }

    [HttpPost]
    public async Task<ActionResult<{EntityName}Dto>> Create(
        Create{EntityName}Request request,
        CancellationToken cancellationToken)
    {
        {EntityName}Dto result = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = result.Id }, result);
    }

    [HttpPut("{id:int}")]
    public async Task<ActionResult<{EntityName}Dto>> Update(
        int id,
        Update{EntityName}Request request,
        CancellationToken cancellationToken)
    {
        return Ok(await service.UpdateAsync(id, request, cancellationToken));
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(id, cancellationToken);
        return NoContent();
    }
}
```

**Notes:**
- Dùng `{id:int}` cho route constraint (đổi sang `{id:guid}` nếu dùng Guid)
- Không cần check null - Service throw `NotFoundException` → `ExceptionHandlingMiddleware` trả 404
- Explicit type cho `result` trong Create

---

## Step 9: Migration

```bash
dotnet ef migrations add Add{EntityName}Entity \
  --project backend/src/StarterKit.Infrastructure \
  --startup-project backend/src/StarterKit.API
```

---

## Step 10: Verify

```bash
dotnet build backend/StarterKit.sln --no-restore -m:1
dotnet run --project backend/src/StarterKit.API
```

Test via Swagger: http://localhost:5000/swagger

---

## Pagination Models

If not exists, create:

**backend/src/StarterKit.Application/Common/Models/PagedResult.cs:**
```csharp
namespace StarterKit.Application.Common.Models;

public sealed record PagedResult<T>(
    IReadOnlyList<T> Items,
    int TotalCount,
    int PageNumber,
    int PageSize)
{
    public int TotalPages => (int)Math.Ceiling(TotalCount / (double)PageSize);
    public bool HasPreviousPage => PageNumber > 1;
    public bool HasNextPage => PageNumber < TotalPages;
}
```

**backend/src/StarterKit.Application/Common/Models/PaginationRequest.cs:**
```csharp
namespace StarterKit.Application.Common.Models;

public sealed record PaginationRequest(int PageNumber = 1, int PageSize = 10);
```

**Add to IRepository<T>:**
```csharp
Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
    int pageNumber, int pageSize, CancellationToken cancellationToken = default);
```

**Implement in GenericRepository:**
```csharp
public async Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
    int pageNumber, int pageSize, CancellationToken cancellationToken = default)
{
    IQueryable<T> query = dbSet.OrderBy(entity => entity.Id);
    int totalCount = await query.CountAsync(cancellationToken);
    List<T> items = await query
        .Skip((pageNumber - 1) * pageSize)
        .Take(pageSize)
        .ToListAsync(cancellationToken);

    return (items, totalCount);
}
```
