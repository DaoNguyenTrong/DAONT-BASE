# Phase 1 — Tenancy Core

## Context

Phase 0 (email/password auth, no global `Account.Role`, UUID v7) is done. Every phase after Phase 1 (Project/ApiKey, rate limiting, public feedback, dashboard feedback) depends on tenant context existing. This phase introduces `Tenant` + `TenantMembership` (`TenantRole`: Owner/Member) so a registered Account can create a Tenant (becoming its sole Owner), and requests can optionally scope themselves to a Tenant via the `X-Tenant-Id` header.

Locked decisions from `.claude/decisions.md` (not up for relitigation here):
- JWT never carries tenant context — `X-Tenant-Id` header only, no re-issue on switch.
- Exactly 1 Owner per Tenant.
- Account↔Tenant is many-to-many via `TenantMembership`, not 1:1.
- No Tenant / invalid header → **empty data**, not 403 (except mutations that require a tenant — none exist yet in Phase 1).
- Invite, transfer-owner, trash/purge (Phase 2) and Project/ApiKey (Phase 3) are out of scope here.

**Scope note:** no endpoint in Phase 1 actually needs `X-Tenant-Id` to serve data (`GetMyTenants` scopes by JWT account id, `GetById` scopes by route id). The middleware + `ICurrentTenantService` are forward infrastructure for Phase 3+ (Projects) — build and verify them as a standalone unit, not through an end-to-end flow that doesn't exist yet.

## Codebase conventions to follow (verified against source)

- Entity pattern (`backend/src/FeedbackHub.Domain/Entities/Account.cs`, `RefreshToken.cs`): `record {E}Params(...)`, `sealed class {E} : BaseEntity<Guid>`, private ctor + `static Create(Params)` (sets `Id = IdGenerator.NewUuidV7()`, delegates to `Update`) + `Update(Params)` (validates, throws `DomainException(DomainMessages.X)`). No navigation properties anywhere in this codebase (confirmed on `RefreshToken.AccountId`) — FKs are plain scalar `Guid` props.
- EF config: one `IEntityTypeConfiguration<T>` per entity under `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/`, auto-discovered via `ApplyConfigurationsFromAssembly` in `AppDbContext.OnModelCreating` — just add the class + a `DbSet<T>` line in `AppDbContext.cs`. Two competing column-naming styles exist (`AccountConfiguration` = implicit PascalCase, `ApiKeyConfiguration` = explicit snake_case `HasColumnName`); **follow `ApiKeyConfiguration`'s explicit-snake_case style** for the new entities — required because item 3 below adds a raw-SQL filtered index that must reference the real column name.
- `TenantRole` is the **first enum in this codebase**. Store as string: `HasConversion<string>().HasMaxLength(20)` (matches the old, now-removed `Account.Role` column shape). No app-side serialization work needed — `ConfigureNewtonsoftJsonOptions.cs` already registers a global `StringEnumConverter { AllowIntegerValues = false }`, so `TenantRole` in DTOs serializes as `"Owner"`/`"Member"` automatically.
- Repository: generic `IRepository<T, TId>` via `IUnitOfWork.Repository<T, TId>()` already covers new entities, no repo code needed. DbContext is NoTracking-globally — call `repository.Update(entity)` after mutating a loaded entity.
- Exceptions: `DomainException`/`NotFoundException`/`ConflictException` etc. in `backend/src/FeedbackHub.Domain/Exceptions/`; messages are localization keys, defined as `const string` in `DomainMessages.cs` (domain-thrown) or `ApplicationMessages.cs` (service-thrown), with matching entries required in both `backend/src/FeedbackHub.Application/Resources/Messages.resx` (vi) and `Messages.en.resx`.
- Controller/Service/DTO/Mapperly pattern: `backend/src/FeedbackHub.API/Controllers/AccountsController.cs` + `backend/src/FeedbackHub.Application/Services/Accounts/*`. `EntityMapper` is a Mapperly `[Mapper] static partial class` in `backend/src/FeedbackHub.Application/Common/Mappings/EntityMapper.cs`.
- The `.claude/skills/crud-entity/SKILL.md` 10-step scaffold is the right backbone but assumes plain generic CRUD with `int` PK — adapt to `Guid`/`BaseEntity<Guid>` and to the "list scoped to current user's memberships" shape (not list-all).

## Implementation

### 1. `Tenant` entity
`backend/src/FeedbackHub.Domain/Entities/Tenant.cs` — `TenantParams(string Name, string? Description = null)`, `Tenant : BaseEntity<Guid>`, standard Create/Update. `Update` throws `DomainException(DomainMessages.TenantNameRequired)` when `Name` is blank. No unique index on `Name` (tenants can share names).

### 2. `TenantRole` enum
`backend/src/FeedbackHub.Domain/Entities/TenantRole.cs` — `enum TenantRole { Owner, Member }`.

### 3. `TenantMembership` entity + single-owner enforcement
`backend/src/FeedbackHub.Domain/Entities/TenantMembership.cs` — `TenantMembershipParams(Guid TenantId, Guid AccountId, TenantRole Role)`, `TenantMembership : BaseEntity<Guid>`. Not generic CRUD — created only as a side effect of `TenantService.CreateAsync` in Phase 1 (invite/remove lands in Phase 2). `Create` validates both ids non-empty (`DomainMessages.TenantIdRequired` new; `DomainMessages.AccountIdRequired` already exists — reuse it).

`backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/TenantMembershipConfiguration.cs`:
- `ToTable("tenant_memberships")`, explicit `HasColumnName` snake_case on every property, mirroring `ApiKeyConfiguration.cs`.
- FKs to both `Tenant` and `Account`, `DeleteBehavior.Cascade` on each (matches `RefreshTokenConfiguration`'s `Account` FK pattern; Postgres allows the two independent cascade paths fine). **Accepted Phase 1 limitation**: deleting an Owner's Account cascade-deletes their membership and can orphan a Tenant with no Owner — closed by Phase 2's transfer-owner flow, not addressed here.
- `HasIndex(m => new { m.TenantId, m.AccountId }).IsUnique()` — no duplicate membership rows.
- Filtered unique index enforcing exactly one Owner per Tenant, added now at DB level (cheap now, standing invariant Phase 2 mutations can't violate later):
  ```csharp
  builder.HasIndex(m => m.TenantId).IsUnique().HasFilter("role = 'Owner'");
  ```
  Must be on `TenantId` alone (not composite with `Role, which would also block a second Member). This only works if the `Role` column is actually named `role` — **after generating the migration, grep it for `filter:` and the `CreateTable` column list and confirm both say `role`.**
- `Role` property: `HasConversion<string>().HasMaxLength(20).IsRequired()`.

No explicit transaction needed in the create flow — `Tenant.Id` is generated client-side before either entity is added to the change tracker, so a single `AddAsync` (Tenant) + `AddAsync` (TenantMembership) + one `SaveChangesAsync` relies on EF's normal FK-ordered insert within its implicit transaction.

### 4. `AppDbContext`
Add `DbSet<Tenant> Tenants => Set<Tenant>();` and `DbSet<TenantMembership> TenantMemberships => Set<TenantMembership>();`. Leave `AuditExcludedEntityTypes` (`= [typeof(RefreshToken)]`) unchanged — both new entities should stay **included** in audit logging (Tenant is Account-like: rare, high-value; TenantMembership rows are access-grant records worth tracking once Phase 2 adds invite/remove/transfer mutations against the same table).

### 5. `X-Tenant-Id` resolution
`backend/src/FeedbackHub.API/Middleware/TenantMiddleware.cs` — modeled on `UserTimeZoneMiddleware.cs` but inverted semantics: missing/invalid header or non-member is never an error, it just means no tenant resolved (never short-circuits, always calls `next`).

```csharp
public sealed class TenantMiddleware(RequestDelegate next)
{
    private const string TenantHeaderName = "X-Tenant-Id";
    public const string TenantIdItemKey = "TenantId";
    public const string TenantRoleItemKey = "TenantRole";

    public async Task InvokeAsync(HttpContext context, IUnitOfWork unitOfWork)
    {
        if (context.User.Identity?.IsAuthenticated != true
            || !context.Request.Headers.TryGetValue(TenantHeaderName, out var headerValues)
            || !Guid.TryParse(headerValues.ToString(), out Guid tenantId)
            || !Guid.TryParse(context.User.FindFirst(ClaimTypes.NameIdentifier)?.Value, out Guid accountId))
        {
            await next(context);
            return;
        }

        TenantMembership? membership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == accountId, context.RequestAborted);

        if (membership is not null)
        {
            context.Items[TenantIdItemKey] = membership.TenantId;
            context.Items[TenantRoleItemKey] = membership.Role;
        }

        await next(context);
    }
}
```
`IUnitOfWork` is injected into `InvokeAsync`, not the constructor — required since this middleware is effectively singleton but `IUnitOfWork` is scoped.

**`Program.cs` placement**: must run *after* `app.UseAuthentication()` (needs `HttpContext.User` populated). Insert between `app.UseAuthorization()` and `app.MapControllers()`:
```csharp
app.UseAuthentication();
app.UseAuthorization();
app.UseMiddleware<TenantMiddleware>();   // new
app.MapControllers();
```
This is a different position than `UserTimeZoneMiddleware` (registered before auth) — intentional, driven by the DB membership lookup needing the authenticated user.

**`ICurrentTenantService`** (mirrors `IUserTimeZoneProvider`/`UserTimeZoneProvider`):
- `backend/src/FeedbackHub.Application/Common/Interfaces/ICurrentTenantService.cs`: `Guid? TenantId { get; }`, `TenantRole? Role { get; }`.
- `backend/src/FeedbackHub.Infrastructure/Services/CurrentTenantService.cs`: reads `httpContextAccessor.HttpContext?.Items[TenantMiddleware.TenantIdItemKey] as Guid?` / `...TenantRoleItemKey] as TenantRole?`.
- Register in `backend/src/FeedbackHub.Infrastructure/Persistence/PersistenceExtensions.cs` alongside the existing `ICurrentUserService`/`IUserTimeZoneProvider` lines: `services.AddScoped<ICurrentTenantService, CurrentTenantService>();`.

### 6. `ITenantService` / `TenantService`
`backend/src/FeedbackHub.Application/Services/Tenants/ITenantService.cs`:
```csharp
Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct);
Task<TenantDto> GetByIdAsync(Guid id, CancellationToken ct);
Task<IReadOnlyList<TenantDto>> GetMyTenantsAsync(CancellationToken ct);
```
No pagination on `GetMyTenants` — a user's tenant count is small in Phase 1.

`TenantService(IUnitOfWork unitOfWork, ICurrentUserService currentUserService)`:
- `CreateAsync`: resolve `accountId` from `currentUserService.UserId` (throw `UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired)` — existing key — if unparsable); `Tenant.Create(request.ToParams())`; `TenantMembership.Create(new(tenant.Id, accountId, TenantRole.Owner))`; add both, one `SaveChangesAsync`; return `EntityMapper.ToDto(tenant, TenantRole.Owner)`.
- `GetByIdAsync(id)`: look up the caller's `TenantMembership` for `(id, accountId)` first — if null, throw `NotFoundException(nameof(Tenant), id)` (**404, not 403** — a non-member can't distinguish "doesn't exist" from "exists but I'm not in it"; also consistent with the empty-not-403 philosophy). Then load the `Tenant` (also 404 if somehow missing). Return `EntityMapper.ToDto(tenant, membership.Role)`.
- `GetMyTenantsAsync`: `ListAsync(m => m.AccountId == accountId)` on `TenantMembership` → if empty, return `[]`. Otherwise batch-load `Tenant`s via `ListAsync(t => tenantIds.Contains(t.Id))` (two round trips — intentional, matches the no-nav-prop convention used everywhere else) and zip with each membership's `Role`.

### 7. `TenantsController`
`backend/src/FeedbackHub.API/Controllers/TenantsController.cs`, `[ApiController][Authorize][Route("api/[controller]")]`:
- `GET /api/tenants` → `GetMyTenantsAsync` (plain array, not `PagedResult`).
- `GET /api/tenants/{id:guid}` → `GetByIdAsync`.
- `POST /api/tenants` → `CreateAsync`, `CreatedAtAction(nameof(GetById), new { id = tenant.Id }, tenant)`.

### 8. DTOs
`backend/src/FeedbackHub.Application/Services/Tenants/TenantDto.cs`:
```csharp
public sealed record TenantDto(Guid Id, string Name, string? Description, TenantRole Role, DateTime CreatedAt, DateTime? UpdatedAt);
```
`Role` is the *caller's* role in that tenant (from `TenantMembership`, not a `Tenant` column) — dashboard needs Owner/Member distinction without a second lookup. Not a pure Mapperly 1:1 mapping since `Role` doesn't come from `Tenant` — add a hand-written composing overload to `EntityMapper.cs`:
```csharp
public static TenantDto ToDto(Tenant tenant, TenantRole role) =>
    new(tenant.Id, tenant.Name, tenant.Description, role, tenant.CreatedAt, tenant.UpdatedAt);
```
`backend/src/FeedbackHub.Application/Services/Tenants/CreateTenantRequest.cs` (DataAnnotations style matching `CreateAccountRequest`):
```csharp
public sealed record CreateTenantRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Name,
    [MaxLength(1000, ErrorMessage = "FieldMaxLength")] string? Description);
```
Plus `EntityMapper.ToParams(this CreateTenantRequest request)`.

### 9. New message keys
`DomainMessages.cs`: add `TenantNameRequired`, `TenantIdRequired`. Add both to `Messages.resx` (vi) and `Messages.en.resx`. The `GetByIdAsync` 404 path reuses the existing `EntityNotFound` key — no new entry needed there. Do **not** add keys for "second owner"/"not a member"/"tenant required for mutation" — no Phase 1 code path reaches them (defer to Phase 2).

### 10. `.claude/rules/authentication.md`
Update to: (a) drop the stale `Roles: User/Admin` table (global role was removed in Phase 0, this file was never updated), replacing it with a note that roles are tenant-scoped (`TenantRole`: Owner/Member) via `X-Tenant-Id` + `ICurrentTenantService`; (b) document `X-Tenant-Id` as an **optional** header (unlike required `X-TimeZone`) — missing/invalid/non-member means no tenant resolved, not a 403.

### 11. Migration
```
dotnet ef migrations add AddTenantAndTenantMembershipEntities \
  --project backend/src/FeedbackHub.Infrastructure --startup-project backend/src/FeedbackHub.API
```

## Verification

1. `dotnet build backend/FEEDBACK-HUB.sln --no-restore -m:1` — must be clean.
2. Grep the generated migration for `filter:` and the `tenant_memberships` `CreateTable` column list — both must say `role` (lowercase). This is the one detail that silently breaks `dotnet ef database update` if the column-naming style isn't applied consistently.
3. `dotnet ef database update ...` then inspect in psql: `\d tenant_memberships` should show the partial unique index (`role = 'Owner'`) alongside the `(tenant_id, account_id)` unique index.
4. Manual API pass (`dotnet run --project backend/src/FeedbackHub.API`, via Scalar/Swagger):
   - Register + login a fresh Account → `POST /api/tenants {name}` → 201, response `role: "Owner"`.
   - `GET /api/tenants` for that user → array containing the created tenant.
   - `GET /api/tenants/{id}` for a tenant the caller is **not** a member of → 404 (not 403).
   - Fresh Account with no tenant → `GET /api/tenants` → `200 []`.
5. `TenantMiddleware`/`ICurrentTenantService` have no HTTP-endpoint consumer yet in Phase 1 — verify at unit-test level instead: a request with a valid member's `X-Tenant-Id` populates `HttpContext.Items["TenantId"]`/`ICurrentTenantService.TenantId`; a missing/garbage/non-member header leaves both null. Add to `backend/tests/FeedbackHub.Application.Tests` (pattern: `AuthServiceTests.cs`).
6. Add `TenantTests.cs`, `TenantMembershipTests.cs` to `backend/tests/FeedbackHub.Domain.Tests` (pattern: `AccountTests.cs`) covering `Create`/`Update` validation, and `TenantServiceTests.cs` to `backend/tests/FeedbackHub.Application.Tests` covering the three service methods (including the not-a-member → 404 path).
7. `dotnet test backend/FEEDBACK-HUB.sln --no-restore -m:1` — must pass.

### Critical files
- `backend/src/FeedbackHub.Domain/Entities/Tenant.cs`, `TenantMembership.cs`, `TenantRole.cs`
- `backend/src/FeedbackHub.Infrastructure/Persistence/Configurations/TenantConfiguration.cs`, `TenantMembershipConfiguration.cs`
- `backend/src/FeedbackHub.Infrastructure/Persistence/AppDbContext.cs`
- `backend/src/FeedbackHub.Application/Services/Tenants/*` (ITenantService, TenantService, TenantDto, CreateTenantRequest)
- `backend/src/FeedbackHub.Application/Common/Interfaces/ICurrentTenantService.cs`
- `backend/src/FeedbackHub.Infrastructure/Services/CurrentTenantService.cs`
- `backend/src/FeedbackHub.API/Middleware/TenantMiddleware.cs`
- `backend/src/FeedbackHub.API/Controllers/TenantsController.cs`
- `backend/src/FeedbackHub.API/Program.cs`
- `backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs`, `backend/src/FeedbackHub.Application/Resources/Messages.resx`, `Messages.en.resx`
- `.claude/rules/authentication.md`
