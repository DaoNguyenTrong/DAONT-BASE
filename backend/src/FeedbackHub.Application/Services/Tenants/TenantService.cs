using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Application.Common.Mappings;
using FeedbackHub.Application.Resources;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Exceptions;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Services.Tenants;

public sealed class TenantService(
    IUnitOfWork unitOfWork,
    ICurrentUserService currentUserService) : ITenantService
{
    public async Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken ct)
    {
        Guid accountId = RequireAccountId();

        Tenant tenant = Tenant.Create(request.ToParams());
        TenantMembership owner = TenantMembership.Create(
            new TenantMembershipParams(tenant.Id, accountId, TenantRole.Owner));

        await unitOfWork.Repository<Tenant, Guid>().AddAsync(tenant, ct);
        await unitOfWork.Repository<TenantMembership, Guid>().AddAsync(owner, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return EntityMapper.ToDto(tenant, TenantRole.Owner);
    }

    public async Task<TenantDto> GetByIdAsync(Guid id, CancellationToken ct)
    {
        Guid accountId = RequireAccountId();

        TenantMembership? membership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == id && m.AccountId == accountId, ct);

        if (membership is null)
            throw new NotFoundException(nameof(Tenant), id);

        Tenant tenant = await unitOfWork.Repository<Tenant, Guid>().GetByIdAsync(id, ct)
            ?? throw new NotFoundException(nameof(Tenant), id);

        return EntityMapper.ToDto(tenant, membership.Role);
    }

    public async Task<IReadOnlyList<TenantDto>> GetMyTenantsAsync(CancellationToken ct)
    {
        Guid accountId = RequireAccountId();

        IReadOnlyList<TenantMembership> memberships = await unitOfWork
            .Repository<TenantMembership, Guid>()
            .ListAsync(m => m.AccountId == accountId, ct);

        if (memberships.Count == 0)
            return [];

        Dictionary<Guid, TenantRole> roleByTenantId = memberships.ToDictionary(m => m.TenantId, m => m.Role);
        IReadOnlyList<Tenant> tenants = await unitOfWork.Repository<Tenant, Guid>()
            .ListAsync(t => roleByTenantId.Keys.Contains(t.Id), ct);

        return tenants.Select(t => EntityMapper.ToDto(t, roleByTenantId[t.Id])).ToList();
    }

    public async Task<TenantMembershipDto> InviteAsync(Guid tenantId, Guid accountToInviteId, CancellationToken ct)
    {
        Guid currentAccountId = RequireAccountId();

        TenantMembership? currentMembership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == currentAccountId, ct);
        if (currentMembership?.Role != TenantRole.Owner)
            throw new ForbiddenException(ApplicationMessages.OnlyTenantOwnerCanInvite);

        Account? targetAccount = await unitOfWork.Repository<Account, Guid>().GetByIdAsync(accountToInviteId, ct);
        if (targetAccount is null)
            throw new NotFoundException(nameof(Account), accountToInviteId);

        TenantMembership? existingMembership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == accountToInviteId, ct);
        if (existingMembership is not null)
            throw new ConflictException(ApplicationMessages.AccountAlreadyMember);

        TenantMembership newMembership = TenantMembership.Create(
            new TenantMembershipParams(tenantId, accountToInviteId, TenantRole.Member));

        await unitOfWork.Repository<TenantMembership, Guid>().AddAsync(newMembership, ct);
        await unitOfWork.SaveChangesAsync(ct);

        return new TenantMembershipDto(tenantId, accountToInviteId, targetAccount.Username, TenantRole.Member, newMembership.CreatedAt);
    }

    public async Task RemoveAsync(Guid tenantId, Guid accountToRemoveId, CancellationToken ct)
    {
        Guid currentAccountId = RequireAccountId();

        TenantMembership? currentMembership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == currentAccountId, ct);
        if (currentMembership?.Role != TenantRole.Owner)
            throw new ForbiddenException(ApplicationMessages.OnlyTenantOwnerCanRemove);

        if (accountToRemoveId == currentAccountId)
            throw new ConflictException(ApplicationMessages.CannotRemoveSelf);

        TenantMembership? targetMembership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == accountToRemoveId, ct);
        if (targetMembership is null)
            throw new NotFoundException(nameof(Account), accountToRemoveId);

        if (targetMembership.Role == TenantRole.Owner)
            throw new ConflictException(ApplicationMessages.CannotRemoveLastOwner);

        unitOfWork.Repository<TenantMembership, Guid>().Delete(targetMembership);
        await unitOfWork.SaveChangesAsync(ct);
    }

    public async Task<TenantMembershipDto> TransferOwnershipAsync(Guid tenantId, Guid newOwnerId, CancellationToken ct)
    {
        Guid currentAccountId = RequireAccountId();

        TenantMembership? currentMembership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == currentAccountId, ct);
        if (currentMembership?.Role != TenantRole.Owner)
            throw new ForbiddenException(ApplicationMessages.OnlyTenantOwnerCanTransfer);

        if (newOwnerId == currentAccountId)
            throw new ConflictException(ApplicationMessages.CannotTransferToSelf);

        TenantMembership? newOwnerMembership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == newOwnerId, ct);
        if (newOwnerMembership is null)
            throw new NotFoundException(nameof(Account), newOwnerId);

        Account newOwnerAccount = await unitOfWork.Repository<Account, Guid>().GetByIdAsync(newOwnerId, ct)
            ?? throw new NotFoundException(nameof(Account), newOwnerId);

        currentMembership.Update(new TenantMembershipParams(tenantId, currentAccountId, TenantRole.Member));
        newOwnerMembership.Update(new TenantMembershipParams(tenantId, newOwnerId, TenantRole.Owner));

        unitOfWork.Repository<TenantMembership, Guid>().Update(currentMembership);
        unitOfWork.Repository<TenantMembership, Guid>().Update(newOwnerMembership);
        await unitOfWork.SaveChangesAsync(ct);

        return new TenantMembershipDto(tenantId, newOwnerId, newOwnerAccount.Username, TenantRole.Owner, newOwnerMembership.UpdatedAt ?? newOwnerMembership.CreatedAt);
    }

    public async Task<IReadOnlyList<TenantMembershipDto>> ListMembersAsync(Guid tenantId, CancellationToken ct)
    {
        Guid currentAccountId = RequireAccountId();

        TenantMembership? currentMembership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == currentAccountId, ct);
        if (currentMembership is null)
            return [];

        IReadOnlyList<TenantMembership> memberships = await unitOfWork.Repository<TenantMembership, Guid>()
            .ListAsync(m => m.TenantId == tenantId, ct);

        Dictionary<Guid, TenantMembership> membershipsByAccountId = memberships.ToDictionary(m => m.AccountId);
        IReadOnlyList<Account> accounts = await unitOfWork.Repository<Account, Guid>()
            .ListAsync(a => membershipsByAccountId.Keys.Contains(a.Id), ct);

        return accounts
            .Select(a => new TenantMembershipDto(tenantId, a.Id, a.Username, membershipsByAccountId[a.Id].Role, membershipsByAccountId[a.Id].CreatedAt))
            .ToList();
    }

    private Guid RequireAccountId() =>
        Guid.TryParse(currentUserService.UserId, out Guid id)
            ? id
            : throw new UnauthorizedException(ApplicationMessages.AuthenticatedUserRequired);
}
