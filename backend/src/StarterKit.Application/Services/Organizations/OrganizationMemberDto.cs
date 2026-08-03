namespace StarterKit.Application.Services.Organizations;

public sealed record OrganizationMemberDto(
    Guid AccountId,
    string AccountName,
    string Email,
    IReadOnlyList<Guid> RoleIds,
    IReadOnlyList<string> RoleNames);
