using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Tenants;

public sealed record InviteMemberRequest(
    [Required(ErrorMessage = "FieldRequired")] Guid AccountId);
