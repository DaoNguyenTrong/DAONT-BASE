using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Services.Tenants;

public sealed record TenantMembershipDto(
    Guid TenantId,
    Guid AccountId,
    string AccountUsername,
    TenantRole Role,
    DateTime CreatedAt);
