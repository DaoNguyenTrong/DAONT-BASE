using System.ComponentModel.DataAnnotations;

namespace StarterKit.Application.Services.Accounts;

public sealed record UpdateAccountRequest(
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength")] string Name,
    [MaxLength(20, ErrorMessage = "FieldMaxLength")] string? Phone,
    [MaxLength(100, ErrorMessage = "FieldMaxLength")] string? Position,
    [MaxLength(500, ErrorMessage = "FieldMaxLength")] string? Address,
    bool Status,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(100, ErrorMessage = "FieldMaxLength")] string Username,
    [Required(ErrorMessage = "FieldRequired"), MaxLength(200, ErrorMessage = "FieldMaxLength"), EmailAddress(ErrorMessage = "FieldEmailAddress")] string Email);
