namespace StarterKit.Application.Services.Organizations;

public sealed record UpdateMemberRolesRequest(IReadOnlyList<Guid> RoleIds);
