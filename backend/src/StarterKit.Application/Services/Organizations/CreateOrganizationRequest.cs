using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Organizations;

public sealed record CreateOrganizationRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Name,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Slug);
