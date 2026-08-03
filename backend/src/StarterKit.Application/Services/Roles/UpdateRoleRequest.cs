using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Roles;

public sealed record UpdateRoleRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Name,
    IReadOnlyList<string> PermissionCodes);
