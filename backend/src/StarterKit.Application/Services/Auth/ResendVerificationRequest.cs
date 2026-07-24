using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Auth;

public sealed record ResendVerificationRequest(
    [Required(ErrorMessage = "FieldRequired"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email);
