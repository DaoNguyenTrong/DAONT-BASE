using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Tenants;

public sealed record TransferOwnershipRequest(
    [Required(ErrorMessage = "FieldRequired")] Guid NewOwnerId);
