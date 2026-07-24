using Microsoft.EntityFrameworkCore.Storage;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Persistence.Repositories;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence.Repositories;

[Collection(nameof(PostgresCollection))]
public sealed class GenericRepositoryTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private GenericRepository<ApiKey, Guid> repository = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        repository = new GenericRepository<ApiKey, Guid>(context);
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private static ApiKey CreateApiKey(string name, DateTime? createdAt = null)
    {
        ApiKey key = ApiKey.Create(new ApiKeyParams(name), name.Length >= 8 ? name[..8] : name.PadRight(8, 'x'), "hash-" + name);
        if (createdAt is not null)
        {
            key.CreatedAt = createdAt.Value;
        }
        return key;
    }

    private async Task SeedAsync(params ApiKey[] keys)
    {
        context.ApiKeys.AddRange(keys);
        await context.SaveChangesAsync();
    }

    // GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsEntity()
    {
        ApiKey key = CreateApiKey("Found Key");
        await SeedAsync(key);

        ApiKey? result = await repository.GetByIdAsync(key.Id);

        Assert.NotNull(result);
        Assert.Equal(key.Id, result!.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        ApiKey? result = await repository.GetByIdAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    // FirstOrDefaultAsync

    [Fact]
    public async Task FirstOrDefaultAsync_MatchingPredicate_ReturnsEntity()
    {
        ApiKey key = CreateApiKey("Predicate Key");
        await SeedAsync(key);

        ApiKey? result = await repository.FirstOrDefaultAsync(k => k.Name == "Predicate Key");

        Assert.NotNull(result);
        Assert.Equal(key.Id, result!.Id);
    }

    // ListAsync

    [Fact]
    public async Task ListAsync_NoPredicate_ReturnsAllSeeded()
    {
        await SeedAsync(CreateApiKey("One"), CreateApiKey("Two"));

        IReadOnlyList<ApiKey> result = await repository.ListAsync();

        Assert.Equal(2, result.Count);
    }

    [Fact]
    public async Task ListAsync_WithPredicate_FiltersResults()
    {
        await SeedAsync(CreateApiKey("Active-1"), CreateApiKey("Active-2"));
        ApiKey inactive = CreateApiKey("Inactive-1");
        inactive.Deactivate();
        await SeedAsync(inactive);

        IReadOnlyList<ApiKey> result = await repository.ListAsync(k => k.IsActive);

        Assert.Equal(2, result.Count);
    }

    // ListPagedAsync (page, size)

    [Fact]
    public async Task ListPagedAsync_PageSize_OrdersByIdAndReportsTotalCount()
    {
        await SeedAsync(CreateApiKey("A"), CreateApiKey("B"), CreateApiKey("C"));

        (IReadOnlyList<ApiKey> items, int totalCount) = await repository.ListPagedAsync(1, 2);

        Assert.Equal(3, totalCount);
        Assert.Equal(2, items.Count);
    }

    // ListPagedAsync (predicate, page, size)

    [Fact]
    public async Task ListPagedAsync_WithPredicate_OrdersByCreatedAtDescending()
    {
        ApiKey older = CreateApiKey("Older", DateTime.UtcNow.AddHours(-1));
        ApiKey newer = CreateApiKey("Newer", DateTime.UtcNow);
        await SeedAsync(older, newer);

        (IReadOnlyList<ApiKey> items, int totalCount) = await repository.ListPagedAsync(k => true, 1, 10);

        Assert.Equal(2, totalCount);
        Assert.Equal("Newer", items[0].Name);
        Assert.Equal("Older", items[1].Name);
    }

    // ListPagedAsync (predicate, searchTerm, searchColumns, page, size) — Npgsql ILike path

    [Fact]
    public async Task ListPagedAsync_SearchTerm_MatchesCaseInsensitivePartialAcrossColumns()
    {
        await SeedAsync(CreateApiKey("Continuous Integration"), CreateApiKey("Deploy Key"));

        (IReadOnlyList<ApiKey> items, int totalCount) = await repository.ListPagedAsync(
            _ => true, "integration", [k => k.Name], 1, 10);

        Assert.Equal(1, totalCount);
        Assert.Equal("Continuous Integration", items[0].Name);
    }

    [Fact]
    public async Task ListPagedAsync_EmptySearchTerm_SkipsFilter()
    {
        await SeedAsync(CreateApiKey("One"), CreateApiKey("Two"));

        (IReadOnlyList<ApiKey> items, int totalCount) = await repository.ListPagedAsync(
            _ => true, null, [k => k.Name], 1, 10);

        Assert.Equal(2, totalCount);
        Assert.Equal(2, items.Count);
    }

    // AddAsync / Update / Delete

    [Fact]
    public async Task AddAsync_ThenSaveChanges_Persists()
    {
        ApiKey key = CreateApiKey("New Key");

        await repository.AddAsync(key);
        await context.SaveChangesAsync();

        ApiKey? reloaded = await repository.GetByIdAsync(key.Id);
        Assert.NotNull(reloaded);
    }

    [Fact]
    public async Task Update_ThenSaveChanges_PersistsChange()
    {
        ApiKey key = CreateApiKey("Original Name");
        await SeedAsync(key);

        key.Deactivate();
        repository.Update(key);
        await context.SaveChangesAsync();

        ApiKey? reloaded = await repository.GetByIdAsync(key.Id);
        Assert.False(reloaded!.IsActive);
    }

    [Fact]
    public async Task Delete_ThenSaveChanges_RemovesEntity()
    {
        ApiKey key = CreateApiKey("To Delete");
        await SeedAsync(key);

        repository.Delete(key);
        await context.SaveChangesAsync();

        ApiKey? reloaded = await repository.GetByIdAsync(key.Id);
        Assert.Null(reloaded);
    }
}
