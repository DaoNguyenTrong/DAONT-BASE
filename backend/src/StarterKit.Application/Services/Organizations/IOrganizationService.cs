namespace StarterKit.Application.Services.Organizations;

public interface IOrganizationService
{
    Task<OrganizationDto> CreateAsync(CreateOrganizationRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationDto>> ListMineAsync(CancellationToken cancellationToken);

    Task<IReadOnlyList<OrganizationMemberDto>> GetMembersAsync(Guid organizationId, CancellationToken cancellationToken);

    Task DeactivateAsync(Guid organizationId, CancellationToken cancellationToken);
}
