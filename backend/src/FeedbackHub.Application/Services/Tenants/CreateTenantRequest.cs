using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Tenants;

public sealed record CreateTenantRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Name,
    [MaxLength(1000, ErrorMessage = "FieldMaxLength")] string? Description);
