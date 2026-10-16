using System.ComponentModel.DataAnnotations;
using StarterKit.Application.Common;

namespace StarterKit.Application.Services.Auth;

/// <remarks>No minimum length on <c>Password</c>: login only compares it with the stored hash (see
/// <see cref="Common.PasswordPolicy"/>).</remarks>
public sealed record LoginRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Username,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(PasswordPolicy.MaxLength, ErrorMessage = "FieldMaxLength")] string Password,
    bool KeepLoggedIn = false);
