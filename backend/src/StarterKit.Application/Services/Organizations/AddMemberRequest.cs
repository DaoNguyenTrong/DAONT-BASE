using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Organizations;

public sealed record AddMemberRequest(
    [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email,
    IReadOnlyList<Guid> RoleIds);
