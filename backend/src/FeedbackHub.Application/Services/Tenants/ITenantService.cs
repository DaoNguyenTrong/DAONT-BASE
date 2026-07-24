namespace FeedbackHub.Application.Services.Tenants;

public interface ITenantService
{
    Task<TenantDto> CreateAsync(CreateTenantRequest request, CancellationToken cancellationToken);

    Task<TenantDto> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantDto>> GetMyTenantsAsync(CancellationToken cancellationToken);

    Task<TenantMembershipDto> InviteAsync(Guid tenantId, Guid accountToInviteId, CancellationToken cancellationToken);

    Task RemoveAsync(Guid tenantId, Guid accountToRemoveId, CancellationToken cancellationToken);

    Task<TenantMembershipDto> TransferOwnershipAsync(Guid tenantId, Guid newOwnerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<TenantMembershipDto>> ListMembersAsync(Guid tenantId, CancellationToken cancellationToken);
}
