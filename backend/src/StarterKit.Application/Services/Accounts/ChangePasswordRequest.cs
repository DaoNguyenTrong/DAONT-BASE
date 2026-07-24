using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Accounts;

public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "FieldRequired"), MinLength(8, ErrorMessage = "FieldMinLength"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string CurrentPassword,
    [Required(ErrorMessage = "FieldRequired"), MinLength(8, ErrorMessage = "FieldMinLength"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string NewPassword);
