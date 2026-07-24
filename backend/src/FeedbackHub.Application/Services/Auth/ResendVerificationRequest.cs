using System.ComponentModel.DataAnnotations;

namespace FeedbackHub.Application.Services.Auth;

public sealed record ResendVerificationRequest(
    [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email);
