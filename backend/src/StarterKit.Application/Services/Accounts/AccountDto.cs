namespace StarterKit.Application.Services.Accounts;

public sealed record AccountDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Position,
    string? Address,
    bool Status,
    string Username,
    string Email,
    bool EmailConfirmed,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
