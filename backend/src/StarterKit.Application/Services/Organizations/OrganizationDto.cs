namespace StarterKit.Application.Services.Organizations;

public sealed record OrganizationDto(
    Guid Id,
    string Name,
    string Slug,
    bool Status,
    IReadOnlyList<string> MyRoleNames,
    IReadOnlyList<string> MyPermissionCodes);
