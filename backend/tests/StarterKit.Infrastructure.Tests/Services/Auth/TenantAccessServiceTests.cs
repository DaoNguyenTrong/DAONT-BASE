using System.Linq.Expressions;
using Microsoft.Extensions.Options;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Common.Settings;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;
using StarterKit.Infrastructure.Services.Auth;

namespace StarterKit.Infrastructure.Tests.Services.Auth;

public class TenantAccessServiceTests
{
    private sealed record Fixture(
        TenantAccessService Service,
        IRepository<OrganizationMember, Guid> MemberRepo,
        IRepository<Organization, Guid> OrganizationRepo,
        ICacheService CacheService);

    private static Fixture CreateFixture(int cacheTtlSeconds = 60)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<OrganizationMember, Guid> memberRepo = Substitute.For<IRepository<OrganizationMember, Guid>>();
        IRepository<Organization, Guid> organizationRepo = Substitute.For<IRepository<Organization, Guid>>();
        unitOfWork.Repository<OrganizationMember, Guid>().Returns(memberRepo);
        unitOfWork.Repository<Organization, Guid>().Returns(organizationRepo);

        ICacheService cacheService = Substitute.For<ICacheService>();
        cacheService.GetOrSetAsync(
                Arg.Any<string>(),
                Arg.Any<string>(),
                Arg.Any<Func<CancellationToken, Task<bool>>>(),
                Arg.Any<TimeSpan?>(),
                Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Func<CancellationToken, Task<bool>> factory = callInfo.Arg<Func<CancellationToken, Task<bool>>>()
                    ?? throw new InvalidOperationException("Factory argument was not provided.");
                return factory(CancellationToken.None);
            });

        IOptions<TenantAccessSettings> options =
            Options.Create(new TenantAccessSettings { CacheTtlSeconds = cacheTtlSeconds });

        TenantAccessService service = new(unitOfWork, cacheService, options);

        return new Fixture(service, memberRepo, organizationRepo, cacheService);
    }

    [Fact]
    public async Task HasActiveAccessAsync_ActiveMemberAndActiveOrganization_ReturnsTrue()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        f.MemberRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(OrganizationMember.Create(
                new OrganizationMemberParams(organizationId, accountId)));

        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>())
            .Returns(Organization.Create(new OrganizationParams("Acme", "acme")));

        bool result = await f.Service.HasActiveAccessAsync(accountId, organizationId);

        Assert.True(result);
    }

    [Fact]
    public async Task HasActiveAccessAsync_NoActiveMember_ReturnsFalse()
    {
        Fixture f = CreateFixture();

        f.MemberRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationMember?)null);

        bool result = await f.Service.HasActiveAccessAsync(Guid.NewGuid(), Guid.NewGuid());

        Assert.False(result);
    }

    [Fact]
    public async Task HasActiveAccessAsync_OrganizationInactive_ReturnsFalse()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        f.MemberRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(OrganizationMember.Create(
                new OrganizationMemberParams(organizationId, accountId)));

        Organization organization = Organization.Create(new OrganizationParams("Acme", "acme"));
        organization.Deactivate();
        f.OrganizationRepo.GetByIdAsync(organizationId, Arg.Any<CancellationToken>()).Returns(organization);

        bool result = await f.Service.HasActiveAccessAsync(accountId, organizationId);

        Assert.False(result);
    }

    [Fact]
    public async Task HasActiveAccessAsync_UsesOrganizationThenAccountCacheKeyOrder()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        f.MemberRepo.FirstOrDefaultAsync(
                Arg.Any<Expression<Func<OrganizationMember, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((OrganizationMember?)null);

        await f.Service.HasActiveAccessAsync(accountId, organizationId);

        await f.CacheService.Received(1).GetOrSetAsync(
            $"tenant-access:{organizationId}",
            accountId.ToString(),
            Arg.Any<Func<CancellationToken, Task<bool>>>(),
            TimeSpan.FromSeconds(60),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateMemberAsync_RemovesExactCacheKey()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();
        Guid accountId = Guid.NewGuid();

        await f.Service.InvalidateMemberAsync(organizationId, accountId);

        await f.CacheService.Received(1)
            .RemoveAsync($"tenant-access:{organizationId}", accountId.ToString(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task InvalidateOrganizationAsync_InvalidatesOrganizationScope()
    {
        Fixture f = CreateFixture();
        Guid organizationId = Guid.NewGuid();

        await f.Service.InvalidateOrganizationAsync(organizationId);

        await f.CacheService.Received(1)
            .InvalidateScopeAsync($"tenant-access:{organizationId}", Arg.Any<CancellationToken>());
    }
}
