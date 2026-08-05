using System.Linq.Expressions;
using System.Text.RegularExpressions;
using NSubstitute;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Application.Services.ApiKeys;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.ApiKeys;

public class ApiKeyServiceTests
{
    private static readonly Guid OrganizationId = Guid.NewGuid();

    private sealed record Fixture(ApiKeyService Service, IRepository<ApiKey, Guid> ApiKeyRepo, IUnitOfWork UnitOfWork);

    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<ApiKey, Guid> apiKeyRepo = Substitute.For<IRepository<ApiKey, Guid>>();
        unitOfWork.Repository<ApiKey, Guid>().Returns(apiKeyRepo);
        ICurrentTenantProvider currentTenantProvider = Substitute.For<ICurrentTenantProvider>();
        currentTenantProvider.OrganizationId.Returns(OrganizationId);

        ApiKeyService service = new(unitOfWork, currentTenantProvider);

        return new Fixture(service, apiKeyRepo, unitOfWork);
    }

    private static ApiKey CreateApiKey(string name = "CI key", DateTime? createdAt = null)
    {
        ApiKey key = ApiKey.Create(new ApiKeyParams(name), "sk_abcd12", "hash", OrganizationId);
        key.CreatedAt = createdAt ?? DateTime.UtcNow;
        return key;
    }

    private static readonly Regex RawKeyFormat = new("^sk_[A-Za-z0-9_-]+$");

    // CreateAsync

    [Fact]
    public async Task CreateAsync_GeneratesFormattedRawKey_AndPersists()
    {
        Fixture f = CreateFixture();
        CreateApiKeyRequest request = new("CI key");

        CreateApiKeyResult result = await f.Service.CreateAsync(request, CancellationToken.None);

        Assert.Matches(RawKeyFormat, result.RawKey);
        Assert.Equal("CI key", result.Key.Name);
        Assert.True(result.Key.IsActive);
        string expectedPrefix = result.RawKey[..8];
        await f.ApiKeyRepo.Received(1).AddAsync(
            Arg.Is<ApiKey>(k => k != null && k.Name == "CI key" && k.KeyPrefix == expectedPrefix),
            Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_TwoCalls_ProduceDifferentRawKeys()
    {
        Fixture f = CreateFixture();
        CreateApiKeyRequest request = new("CI key");

        CreateApiKeyResult first = await f.Service.CreateAsync(request, CancellationToken.None);
        CreateApiKeyResult second = await f.Service.CreateAsync(request, CancellationToken.None);

        Assert.NotEqual(first.RawKey, second.RawKey);
    }

    // GetAllAsync

    [Fact]
    public async Task GetAllAsync_Empty_ReturnsEmptyList()
    {
        Fixture f = CreateFixture();
        f.ApiKeyRepo.ListAsync(Arg.Any<Expression<Func<ApiKey, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ApiKey>)[]);

        IReadOnlyList<ApiKeyDto> result = await f.Service.GetAllAsync(CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetAllAsync_OrdersByCreatedAtDescending()
    {
        Fixture f = CreateFixture();
        ApiKey older = CreateApiKey("Older", DateTime.UtcNow.AddHours(-1));
        ApiKey newer = CreateApiKey("Newer", DateTime.UtcNow);
        f.ApiKeyRepo.ListAsync(Arg.Any<Expression<Func<ApiKey, bool>>>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<ApiKey>)[older, newer]);

        IReadOnlyList<ApiKeyDto> result = await f.Service.GetAllAsync(CancellationToken.None);

        Assert.Equal(2, result.Count);
        Assert.Equal("Newer", result[0].Name);
        Assert.Equal("Older", result[1].Name);
    }

    // DeactivateAsync

    [Fact]
    public async Task DeactivateAsync_NotFound_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Guid id = Guid.NewGuid();
        f.ApiKeyRepo.GetByIdAsync(id, Arg.Any<CancellationToken>()).Returns((ApiKey?)null);

        await ApplicationAssert.AssertNotFoundAsync<ApiKey>(id, () => f.Service.DeactivateAsync(id, CancellationToken.None));
    }

    [Fact]
    public async Task DeactivateAsync_Found_DeactivatesAndSaves()
    {
        Fixture f = CreateFixture();
        ApiKey key = CreateApiKey();
        f.ApiKeyRepo.GetByIdAsync(key.Id, Arg.Any<CancellationToken>()).Returns(key);

        await f.Service.DeactivateAsync(key.Id, CancellationToken.None);

        Assert.False(key.IsActive);
        f.ApiKeyRepo.Received(1).Update(key);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
}
