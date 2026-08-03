using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;

namespace StarterKit.Infrastructure.Services.Auth;

internal sealed class PermissionResolver(
    AppDbContext dbContext,
    ICacheService cacheService,
    IOptions<PermissionResolverSettings> options) : IPermissionResolver
{
    private readonly PermissionResolverSettings settings = options.Value;

    public Task<IReadOnlySet<string>> GetEffectivePermissionsAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default)
    {
        return cacheService.GetOrSetAsync<IReadOnlySet<string>>(
            CacheKey(organizationId, accountId),
            async ct =>
            {
                OrganizationMember? member = await dbContext.OrganizationMembers.AsNoTracking()
                    .FirstOrDefaultAsync(
                        m => m.OrganizationId == organizationId && m.AccountId == accountId && m.IsActive, ct);

                if (member is null)
                {
                    return new HashSet<string>();
                }

                List<Role> roles = await (
                    from memberRole in dbContext.OrganizationMemberRoles.AsNoTracking()
                    join role in dbContext.Roles.AsNoTracking() on memberRole.RoleId equals role.Id
                    where memberRole.OrganizationMemberId == member.Id
                    select role)
                    .ToListAsync(ct);

                if (roles.Any(role => role.SystemRoleKind == SystemRoleKind.Owner))
                {
                    return Permissions.All.ToHashSet();
                }

                List<Guid> roleIds = roles.Select(role => role.Id).ToList();

                if (roleIds.Count == 0)
                {
                    return new HashSet<string>();
                }

                List<string> codes = await dbContext.RolePermissions.AsNoTracking()
                    .Where(rolePermission => roleIds.Contains(rolePermission.RoleId))
                    .Select(rolePermission => rolePermission.PermissionCode)
                    .Distinct()
                    .ToListAsync(ct);

                return codes.ToHashSet();
            },
            TimeSpan.FromSeconds(settings.CacheTtlSeconds),
            cancellationToken);
    }

    public Task InvalidateMemberAsync(
        Guid organizationId, Guid accountId, CancellationToken cancellationToken = default) =>
        cacheService.RemoveAsync(CacheKey(organizationId, accountId), cancellationToken);

    public Task InvalidateOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken = default) =>
        cacheService.RemoveByPrefixAsync($"permissions:{organizationId}:", cancellationToken);

    private static string CacheKey(Guid organizationId, Guid accountId) =>
        $"permissions:{organizationId}:{accountId}";
}
