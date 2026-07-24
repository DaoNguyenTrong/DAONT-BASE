using System.ComponentModel.DataAnnotations;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Auth;

public sealed record RegisterRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Name,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Username,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email,
    [Required(ErrorMessage = "FieldRequired"), MinLength(8, ErrorMessage = "FieldMinLength"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Password,
    [MaxLength(20, ErrorMessage = "FieldMaxLength")] string? Phone = null,
    [MaxLength(100, ErrorMessage = "FieldMaxLength")] string? Position = null,
    [MaxLength(500, ErrorMessage = "FieldMaxLength")] string? Address = null)
{
    public AccountParams ToParams() =>
        new(Name, Username, Email, Phone, Position, Address, true);
}
