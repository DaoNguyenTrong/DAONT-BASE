using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Application.Common.Interfaces;

public interface ICurrentTenantService
{
    Guid? TenantId { get; }

    TenantRole? Role { get; }
}
