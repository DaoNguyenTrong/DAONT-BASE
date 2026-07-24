using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Auth;

public sealed record ExternalLoginRequest(
    [Required(ErrorMessage = "FieldRequired")] string Credential);
