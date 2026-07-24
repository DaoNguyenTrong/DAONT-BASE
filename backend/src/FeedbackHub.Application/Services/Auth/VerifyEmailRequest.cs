using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Auth;

public sealed record VerifyEmailRequest(
    [Required(ErrorMessage = "FieldRequired")] string Token);
