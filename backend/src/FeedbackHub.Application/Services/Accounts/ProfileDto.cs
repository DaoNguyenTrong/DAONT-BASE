namespace FeedbackHub.Application.Services.Accounts;

public sealed record ProfileDto(
    Guid Id,
    string Name,
    string? Phone,
    string? Position,
    string? Address,
    string Username,
    string Email,
    bool EmailConfirmed,
    bool HasPassword);
