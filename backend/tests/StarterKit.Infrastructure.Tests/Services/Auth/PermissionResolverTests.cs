using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Authorization;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Services.Auth;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

[Collection(nameof(PostgresCollection))]
public sealed class PermissionResolverTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private ICacheService cacheService = null!;
    private PermissionResolver resolver = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();

        cacheService = Substitute.For<ICacheService>();
        cacheService.GetOrSetAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<IReadOnlySet<string>>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<CancellationToken, Task<IReadOnlySet<string>>> factory =
                    callInfo.Arg<Func<CancellationToken, Task<IReadOnlySet<string>>>>()
                    ?? throw new InvalidOperationException("Factory argument was not provided.");
                return factory(CancellationToken.None);
            });

        resolver = new PermissionResolver(context, cacheService, Options.Create(new PermissionResolverSettings()));
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private async Task<(Organization Organization, OrganizationMember Member)> SeedMemberAsync(SystemRoleKind kind)
    {
        Organization organization = Organization.Create(new OrganizationParams("Acme", $"acme-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);

        Dictionary<SystemRoleKind, Role> roles = [];

        foreach (SystemRoleKind k in Enum.GetValues<SystemRoleKind>())
        {
            Role role = Role.Create(new RoleParams(organization.Id, k.ToString(), k));
            context.Roles.Add(role);
            roles[k] = role;

            if (k == SystemRoleKind.Admin)
            {
                context.RolePermissions.Add(
                    RolePermission.Create(new RolePermissionParams(role.Id, Permissions.OrganizationMembersManage)));
            }
        }

        Account account = Account.Create(
            new AccountParams("Member", $"member-{Guid.NewGuid():N}", $"member-{Guid.NewGuid():N}@example.com"));
        context.Accounts.Add(account);

        OrganizationMember member = OrganizationMember.Create(
            new OrganizationMemberParams(organization.Id, account.Id));
        context.OrganizationMembers.Add(member);
        context.OrganizationMemberRoles.Add(
            OrganizationMemberRole.Create(new OrganizationMemberRoleParams(member.Id, roles[kind].Id)));

        await context.SaveChangesAsync();

        return (organization, member);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_Owner_ReturnsAllPermissionsWithoutStoredRows()
    {
        (Organization organization, OrganizationMember member) = await SeedMemberAsync(SystemRoleKind.Owner);

        IReadOnlySet<string> permissions =
            await resolver.GetEffectivePermissionsAsync(organization.Id, member.AccountId);

        Assert.Equal(Permissions.All.ToHashSet(), permissions);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_Admin_ReturnsSeededPermission()
    {
        (Organization organization, OrganizationMember member) = await SeedMemberAsync(SystemRoleKind.Admin);

        IReadOnlySet<string> permissions =
            await resolver.GetEffectivePermissionsAsync(organization.Id, member.AccountId);

        Assert.Equal(new HashSet<string> { Permissions.OrganizationMembersManage }, permissions);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_Member_ReturnsEmptySet()
    {
        (Organization organization, OrganizationMember member) = await SeedMemberAsync(SystemRoleKind.Member);

        IReadOnlySet<string> permissions =
            await resolver.GetEffectivePermissionsAsync(organization.Id, member.AccountId);

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_NotAMember_ReturnsEmptySet()
    {
        Organization organization = Organization.Create(new OrganizationParams("Acme", $"acme-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();

        IReadOnlySet<string> permissions =
            await resolver.GetEffectivePermissionsAsync(organization.Id, Guid.NewGuid());

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task GetEffectivePermissionsAsync_InactiveMember_ReturnsEmptySet()
    {
        (Organization organization, OrganizationMember member) = await SeedMemberAsync(SystemRoleKind.Admin);
        member.Deactivate();
        context.OrganizationMembers.Update(member);
        await context.SaveChangesAsync();

        IReadOnlySet<string> permissions =
            await resolver.GetEffectivePermissionsAsync(organization.Id, member.AccountId);

        Assert.Empty(permissions);
    }

    [Fact]
    public async Task InvalidateMemberAsync_RemovesExactCacheKey()
    {
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        await resolver.InvalidateMemberAsync(organizationId, accountId);

        await cacheService.Received(1)
            .RemoveAsync($"permissions:{organizationId}", accountId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateOrganizationAsync_InvalidatesOrganizationScope()
    {
        Guid organizationId = Guid.NewGuid();

        await resolver.InvalidateOrganizationAsync(organizationId);

        await cacheService.Received(1)
            .InvalidateScopeAsync($"permissions:{organizationId}", Arg.Any<CancellationToken>());
    }
}
