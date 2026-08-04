namespace StarterKit.Application.Services.Organizations;

public interface IOrganizationMembershipService
{
    Task AddMemberAsync(Guid organizationId, AddMemberRequest request, CancellationToken cancellationToken);

    Task UpdateMemberRolesAsync(
        Guid organizationId,
        Guid accountId,
        UpdateMemberRolesRequest request,
        CancellationToken cancellationToken);

    Task RemoveMemberAsync(Guid organizationId, Guid accountId, CancellationToken cancellationToken);
}
