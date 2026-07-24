using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Tenants;

public sealed record TenantDto(
    Guid Id,
    string Name,
    string? Description,
    TenantRole Role,
    DateTime CreatedAt,
    DateTime? UpdatedAt);
