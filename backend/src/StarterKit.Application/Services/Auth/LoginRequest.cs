using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Auth;

public sealed record LoginRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Username,
    [Required(ErrorMessage = "FieldRequired"), MinLength(8, ErrorMessage = "FieldMinLength"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Password,
    bool KeepLoggedIn = false);
