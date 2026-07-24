using FeedbackHub.Application.Common.Interfaces;
using FeedbackHub.Domain.Entities;
using Microsoft.AspNetCore.Http;

namespace FeedbackHub.Infrastructure.Services;

public sealed class CurrentTenantService(IHttpContextAccessor httpContextAccessor) : ICurrentTenantService
{
    private const string TenantIdItemKey = "TenantId";
    private const string TenantRoleItemKey = "TenantRole";

    public Guid? TenantId =>
        httpContextAccessor.HttpContext?.Items[TenantIdItemKey] as Guid?;

    public TenantRole? Role =>
        httpContextAccessor.HttpContext?.Items[TenantRoleItemKey] as TenantRole?;
}
