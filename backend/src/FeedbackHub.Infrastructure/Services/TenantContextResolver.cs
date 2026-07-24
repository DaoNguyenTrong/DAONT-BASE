using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Infrastructure.Services;

public sealed class TenantContextResolver(IUnitOfWork unitOfWork) : ITenantContextResolver
{
    public async Task<(Guid TenantId, TenantRole Role)?> ResolveAsync(
        Guid tenantId,
        Guid accountId,
        CancellationToken ct)
    {
        TenantMembership? membership = await unitOfWork.Repository<TenantMembership, Guid>()
            .FirstOrDefaultAsync(m => m.TenantId == tenantId && m.AccountId == accountId, ct);

        if (membership is null)
            return null;

        return (membership.TenantId, membership.Role);
    }
}
