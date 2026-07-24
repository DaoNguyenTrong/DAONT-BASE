using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Auth;

public sealed record VerifyEmailRequest(
    [Required(ErrorMessage = "FieldRequired")] string Token);
