using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Auth;

public sealed record ExternalLoginRequest(
    [Required(ErrorMessage = "FieldRequired")] string Credential);
