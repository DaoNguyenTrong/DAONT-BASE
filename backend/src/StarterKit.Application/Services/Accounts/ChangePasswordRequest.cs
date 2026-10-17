using System.ComponentModel.DataAnnotations;
using StarterKit.Application.Common;

namespace StarterKit.Application.Services.Accounts;

/// <remarks>Only <c>NewPassword</c> follows <see cref="PasswordPolicy"/>; <c>CurrentPassword</c> is compared with
/// the stored hash and may predate the policy.</remarks>
public sealed record ChangePasswordRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(PasswordPolicy.MaxLength, ErrorMessage = "FieldMaxLength")] string CurrentPassword,
    [Required(ErrorMessage = "FieldRequired"),
     MinLength(PasswordPolicy.MinLength, ErrorMessage = "FieldMinLength"),
     MaxLength(PasswordPolicy.MaxLength, ErrorMessage = "FieldMaxLength")] string NewPassword);
