namespace StarterKit.Application.Services.Roles;

public sealed record RoleDto(Guid Id, string Name, bool IsSystem, IReadOnlyList<string> PermissionCodes);
