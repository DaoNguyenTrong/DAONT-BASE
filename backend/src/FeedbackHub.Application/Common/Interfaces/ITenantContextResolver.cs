using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Common.Interfaces;

public interface ITenantContextResolver
{
    Task<(Guid TenantId, TenantRole Role)?> ResolveAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken ct);
}
