# Phase 2 — Membership Operations

## Context

Phase 1 (Tenancy core) is complete with `Tenant`, `TenantMembership`, and `X-Tenant-Id` middleware. Database constraints ensure exactly 1 Owner per Tenant via filtered unique index.

Phase 2 adds member management: Invite, Remove, TransferOwnership, Trash/Purge. This enables multi-user tenants and sets up access control for Phase 3+ (Projects/ApiKey scoping).

**Key prior decisions locked in (from .claude/decisions.md):**
- Exactly 1 Owner per Tenant (DB constraint + app validation)
- Invite = add existing Account as Member (no pending invite in MVP)
- Transfer Owner = explicit reassignment (old Owner → Member, new → Owner)
- Trash = soft-delete after 30 days via background job
- Authorization: ForbiddenException for role denials (403, not 404)

---

## Design

### 1. Domain Layer — TenantMembership Extensions

**No new entities.** TenantMembership already exists with TenantId, AccountId, Role.

**Add to `TenantMembership.cs`:**
- `Update(TenantMembershipParams p)` method to allow role changes (currently only has `Create`)
  - Validate new role non-empty
  - Throw `DomainException(DomainMessages.TenantRoleRequired)` if invalid

**No changes to `Tenant.cs` yet** — soft-delete (TrashedAt field) deferred to Phase 2's trash feature.

### 2. Domain Layer — Exception Messages

**Add to `backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs`:**
```csharp
public const string TenantRoleRequired = nameof(TenantRoleRequired);
```

**Add to `backend/src/FeedbackHub.Application/Resources/ApplicationMessages.cs`:**
```csharp
public const string OnlyTenantOwnerCanInvite = nameof(OnlyTenantOwnerCanInvite);
public const string OnlyTenantOwnerCanRemove = nameof(OnlyTenantOwnerCanRemove);
public const string OnlyTenantOwnerCanTransfer = nameof(OnlyTenantOwnerCanTransfer);
public const string CannotRemoveLastOwner = nameof(CannotRemoveLastOwner);
public const string AccountAlreadyMember = nameof(AccountAlreadyMember);
public const string AccountNotFound = nameof(AccountNotFound);
public const string CannotTransferToSelf = nameof(CannotTransferToSelf);
public const string CannotRemoveSelf = nameof(CannotRemoveSelf);
```

**Add translations to `Messages.resx` (vi) + `Messages.en.resx`:**
- TenantRoleRequired: "Vai trò tổ chức là bắt buộc." / "Tenant role is required."
- OnlyTenantOwnerCanInvite: "Chỉ chủ sở hữu tổ chức có thể mời thành viên." / "Only tenant owner can invite members."
- OnlyTenantOwnerCanRemove: "Chỉ chủ sở hữu tổ chức có thể xóa thành viên." / "Only tenant owner can remove members."
- OnlyTenantOwnerCanTransfer: "Chỉ chủ sở hữu tổ chức có thể chuyển quyền." / "Only tenant owner can transfer ownership."
- CannotRemoveLastOwner: "Không thể xóa chủ sở hữu cuối cùng." / "Cannot remove the last owner."
- AccountAlreadyMember: "Tài khoản đã là thành viên của tổ chức." / "Account is already a member of this tenant."
- AccountNotFound: "Tài khoản không tìm thấy." / "Account not found."
- CannotTransferToSelf: "Không thể chuyển quyền cho chính mình." / "Cannot transfer ownership to yourself."
- CannotRemoveSelf: "Không thể xóa chính mình khỏi tổ chức." / "Cannot remove yourself from the tenant."

### 3. Application Layer — Service Interface & Implementation

**File: `backend/src/FeedbackHub.Application/Services/Tenants/ITenantService.cs`**

Add methods (extend existing interface):
```csharp
// Existing methods: CreateAsync, GetByIdAsync, GetMyTenantsAsync

// NEW in Phase 2:
Task<TenantMembershipDto> InviteAsync(
    Guid tenantId,
    Guid accountToInviteId,
    CancellationToken cancellationToken);

Task RemoveAsync(
    Guid tenantId,
    Guid accountToRemoveId,
    CancellationToken cancellationToken);

Task TransferOwnershipAsync(
    Guid tenantId,
    Guid newOwnerId,
    CancellationToken cancellationToken);

Task ListMembersAsync(
    Guid tenantId,
    CancellationToken cancellationToken);
```

**File: `backend/src/FeedbackHub.Application/Services/Tenants/TenantService.cs`**

**InviteAsync(Guid tenantId, Guid accountToInviteId, CancellationToken ct):**
1. Require authenticated user (fail-fast)
2. Query current user's membership in tenantId → validate Owner role
3. If not Owner, throw `ForbiddenException(ApplicationMessages.OnlyTenantOwnerCanInvite)` (403)
4. Query if accountToInviteId is a valid Account → if not found, throw `NotFoundException(nameof(Account), accountToInviteId)`
5. Query if accountToInviteId already member of tenantId
6. If already member, throw `ConflictException(ApplicationMessages.AccountAlreadyMember)` (409)
7. Create `TenantMembership.Create(new(tenantId, accountToInviteId, TenantRole.Member))`
8. Add to repository, `SaveChangesAsync`, return `EntityMapper.ToDto(membership)`
9. (Note: DB unique constraint on (tenant_id, account_id) prevents race condition)

**RemoveAsync(Guid tenantId, Guid accountToRemoveId, CancellationToken ct):**
1. Require authenticated user
2. Query current user's membership → validate Owner
3. If not Owner, throw `ForbiddenException(OnlyTenantOwnerCanRemove)` (403)
4. If accountToRemoveId == currentAccountId, throw `ConflictException(CannotRemoveSelf)` (409)
5. Query membership of accountToRemoveId in tenantId
6. If not found, throw `NotFoundException(nameof(Account), accountToRemoveId)` — or custom "not a member"
7. If membership.Role == Owner, throw `ConflictException(CannotRemoveLastOwner)` (409) — only owner exists
8. Delete membership from repository, `SaveChangesAsync`
9. (No DTO return — 204 No Content from controller)

**TransferOwnershipAsync(Guid tenantId, Guid newOwnerId, CancellationToken ct):**
1. Require authenticated user
2. Query current user's membership → validate Owner
3. If not Owner, throw `ForbiddenException(OnlyTenantOwnerCanTransfer)` (403)
4. If newOwnerId == currentAccountId, throw `ConflictException(CannotTransferToSelf)` (409)
5. Query newOwner's membership in tenantId
6. If not found, throw `NotFoundException(nameof(Account), newOwnerId)` — must already be member
7. Query all memberships in tenantId with role == Owner (should be exactly 1: currentUser)
8. In single transaction:
   - Update old owner membership: Role = Member
   - Update new owner membership: Role = Owner
   - Call `repository.Update()` for both, single `SaveChangesAsync()`
9. Return `EntityMapper.ToDto(newOwnerMembership)`
10. (DB constraint enforces single Owner per tenant — if races exist, constraint violation → ConflictException at DB layer)

**ListMembersAsync(Guid tenantId, CancellationToken ct):**
1. Require authenticated user
2. Query current user's membership in tenantId → validate is member (Owner or Member)
3. If not member, return empty list (consistent with "no tenant → empty list" philosophy)
4. Query all memberships in tenantId (no pagination in P2)
5. Load associated Account details for each membership (TenantMembership has no nav prop; join manually or two-query)
6. Return list of TenantMembershipDto(AccountId, AccountName?, Role, CreatedAt)

---

### 4. DTOs

**File: `backend/src/FeedbackHub.Application/Services/Tenants/TenantMembershipDto.cs`** (new)
```csharp
public sealed record TenantMembershipDto(
    Guid TenantId,
    Guid AccountId,
    string AccountUsername,  // For UI display
    TenantRole Role,
    DateTime CreatedAt);
```

**Extend existing request DTOs:**
- `backend/src/FeedbackHub.Application/Services/Tenants/InviteMemberRequest.cs`:
  ```csharp
  [Required(ErrorMessage = "FieldRequired")] Guid AccountId,
  ```
  (Don't pass role in request; always Member for invite)

---

### 5. API Layer

**File: `backend/src/FeedbackHub.API/Controllers/TenantsController.cs`**

Add endpoints:
```csharp
[HttpPost("{id:guid}/members")]
public async Task<ActionResult<TenantMembershipDto>> InviteMember(
    Guid id,
    InviteMemberRequest request,
    CancellationToken cancellationToken)
{
    TenantMembershipDto membership = await tenantService.InviteAsync(id, request.AccountId, cancellationToken);
    return CreatedAtAction(null, membership);  // or CreatedAtAction(nameof(ListMembers), ...)
}

[HttpDelete("{id:guid}/members/{accountId:guid}")]
public async Task<IActionResult> RemoveMember(
    Guid id,
    Guid accountId,
    CancellationToken cancellationToken)
{
    await tenantService.RemoveAsync(id, accountId, cancellationToken);
    return NoContent();
}

[HttpPost("{id:guid}/transfer-owner")]
[ProducesResponseType(typeof(TenantMembershipDto), StatusCodes.Status200OK)]
public async Task<ActionResult<TenantMembershipDto>> TransferOwnership(
    Guid id,
    TransferOwnershipRequest request,
    CancellationToken cancellationToken)
{
    TenantMembershipDto newOwner = await tenantService.TransferOwnershipAsync(id, request.NewOwnerId, cancellationToken);
    return Ok(newOwner);
}

[HttpGet("{id:guid}/members")]
[ProducesResponseType(typeof(IReadOnlyList<TenantMembershipDto>), StatusCodes.Status200OK)]
public async Task<ActionResult<IReadOnlyList<TenantMembershipDto>>> ListMembers(
    Guid id,
    CancellationToken cancellationToken)
{
    IReadOnlyList<TenantMembershipDto> members = await tenantService.ListMembersAsync(id, cancellationToken);
    return Ok(members);
}
```

**Request DTOs:**
```csharp
public sealed record InviteMemberRequest(
    [Required(ErrorMessage = "FieldRequired")] Guid AccountId);

public sealed record TransferOwnershipRequest(
    [Required(ErrorMessage = "FieldRequired")] Guid NewOwnerId);
```

---

### 6. Mapperly Updates

**File: `backend/src/FeedbackHub.Application/Common/Mappings/EntityMapper.cs`**

Add hand-written mapping (TenantMembership → DTO requires Account.Username join):
```csharp
// Requires manual join; Mapperly cannot auto-map
public static TenantMembershipDto ToDto(TenantMembership membership, string accountUsername) =>
    new(membership.TenantId, membership.AccountId, accountUsername, membership.Role, membership.CreatedAt);

// Requests → Params (if any)
public static InviteMemberRequest ToRequest(this Guid accountId) => 
    new(accountId);  // Trivial, optional
```

---

### 7. Infrastructure — Migrations & Background Jobs

**File: `backend/src/FeedbackHub.Infrastructure/Migrations/[timestamp]_AddTenantMembershipOperations.cs`**

**NOT NEEDED for Phase 2a (Invite, Remove, Transfer)** — schema unchanged.

**Add later in Phase 2b (Trash/Purge):**
- Add `TrashedAt` column to `tenants` table (nullable DateTime)
- Index on `TrashedAt` for purge queries
- Background job for purge (separate work)

---

### 8. Tests

**File: `backend/tests/FeedbackHub.Application.Tests/Services/Tenants/TenantServiceTests.cs`** (new)

Use same pattern as `AuthServiceTests.cs` (NSubstitute + xUnit + Fixture):

**Test categories:**

1. **InviteAsync — Success**
   - ✓ Owner invites existing account → Member membership created

2. **InviteAsync — Failures**
   - ✗ Non-owner tries to invite → ForbiddenException
   - ✗ Target account doesn't exist → NotFoundException
   - ✗ Target already member → ConflictException
   - ✗ Unauthenticated → UnauthorizedException

3. **RemoveAsync — Success**
   - ✓ Owner removes member → membership deleted

4. **RemoveAsync — Failures**
   - ✗ Non-owner tries to remove → ForbiddenException
   - ✗ Try to remove self → ConflictException
   - ✗ Try to remove only owner → ConflictException
   - ✗ Target not a member → NotFoundException
   - ✗ Unauthenticated → UnauthorizedException

5. **TransferOwnershipAsync — Success**
   - ✓ Owner transfers to existing member → old owner → Member, new owner → Owner

6. **TransferOwnershipAsync — Failures**
   - ✗ Non-owner tries to transfer → ForbiddenException
   - ✗ Try to transfer to self → ConflictException
   - ✗ Target not a member → NotFoundException
   - ✗ Unauthenticated → UnauthorizedException

7. **ListMembersAsync**
   - ✓ Member can list all members (authorization check: is member)
   - ✓ Non-member lists empty (consistent with empty-not-403)

---

## Verification

### Build & Test
```bash
dotnet build backend/FEEDBACK-HUB.sln --no-restore -m:1
dotnet test backend/FEEDBACK-HUB.sln --no-restore -m:1
```

Must pass: all tests green, zero warnings/errors.

### Manual API Testing (via Swagger/Scalar)

1. **Prerequisite:** Two registered accounts (Account A = Owner, Account B = new member)

2. **Invite Account B to Tenant of Account A:**
   ```bash
   POST /api/tenants/{tenantId}/members
   Authorization: Bearer {tokenA}
   X-TimeZone: Asia/Ho_Chi_Minh
   Content-Type: application/json

   { "accountId": "{accountBId}" }
   ```
   Expected: 201 Created, membership DTO returned

3. **List members:**
   ```bash
   GET /api/tenants/{tenantId}/members
   Authorization: Bearer {tokenA}
   X-TimeZone: Asia/Ho_Chi_Minh
   ```
   Expected: 200 OK, array with both accounts

4. **Account B tries to invite (should fail):**
   ```bash
   POST /api/tenants/{tenantId}/members
   Authorization: Bearer {tokenB}
   X-TimeZone: Asia/Ho_Chi_Minh

   { "accountId": "{accountCId}" }
   ```
   Expected: 403 Forbidden, "Only tenant owner can invite members."

5. **Transfer ownership:**
   ```bash
   POST /api/tenants/{tenantId}/transfer-owner
   Authorization: Bearer {tokenA}
   X-TimeZone: Asia/Ho_Chi_Minh

   { "newOwnerId": "{accountBId}" }
   ```
   Expected: 200 OK, Account B is now Owner

6. **List members again:**
   ```bash
   GET /api/tenants/{tenantId}/members
   ```
   Expected: Account A is Member, Account B is Owner

7. **Account A tries to remove themselves (should fail):**
   ```bash
   DELETE /api/tenants/{tenantId}/members/{accountAId}
   Authorization: Bearer {tokenA}
   ```
   Expected: 409 Conflict, "Cannot remove yourself."

8. **Account B (new Owner) removes Account A:**
   ```bash
   DELETE /api/tenants/{tenantId}/members/{accountAId}
   Authorization: Bearer {tokenB}
   ```
   Expected: 204 No Content, Account A membership deleted

---

## Critical Files

**Domain:**
- `backend/src/FeedbackHub.Domain/Entities/TenantMembership.cs` — add Update() method
- `backend/src/FeedbackHub.Domain/Exceptions/DomainMessages.cs` — add TenantRoleRequired

**Application:**
- `backend/src/FeedbackHub.Application/Services/Tenants/ITenantService.cs` — extend interface
- `backend/src/FeedbackHub.Application/Services/Tenants/TenantService.cs` — implement 4 new methods
- `backend/src/FeedbackHub.Application/Services/Tenants/TenantMembershipDto.cs` — new DTO
- `backend/src/FeedbackHub.Application/Services/Tenants/InviteMemberRequest.cs` — new request DTO
- `backend/src/FeedbackHub.Application/Services/Tenants/TransferOwnershipRequest.cs` — new request DTO
- `backend/src/FeedbackHub.Application/Common/Mappings/EntityMapper.cs` — add ToDto(membership, username)
- `backend/src/FeedbackHub.Application/Resources/ApplicationMessages.cs` — add auth messages
- `backend/src/FeedbackHub.Application/Resources/Messages.resx` + `.en.resx` — translations

**API:**
- `backend/src/FeedbackHub.API/Controllers/TenantsController.cs` — add 4 endpoints

**Tests:**
- `backend/tests/FeedbackHub.Application.Tests/Services/Tenants/TenantServiceTests.cs` — new comprehensive test suite

**Documentation:**
- `docs/api-tenants.md` — update with new endpoints
- `docs/integration-tenants.md` — update with new operations

---

## Deferred to Phase 2b or Later

- **Soft-delete (TrashedAt)** — add column, query filters, soft-delete endpoints
- **Background job (Purge 30 days)** — scheduled task to permanently delete soft-deleted tenants
- **Pagination on ListMembersAsync** — only in P2+ if needed
- **Member role expansion** (Viewer, Editor, etc.) — Phase 3+ per MVP roadmap
- **Pending invite** (email-based invite for non-members) — Phase 2+ per roadmap
- **Audit logging for membership changes** — can integrate when audit is needed

---

## DI & Configuration

No new DI registrations needed — `ITenantService` already registered.

**Optional config for Phase 2b:** BackgroundJob configuration for purge task.
