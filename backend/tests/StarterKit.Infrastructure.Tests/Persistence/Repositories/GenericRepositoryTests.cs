using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Persistence.Repositories;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence.Repositories;

[Collection(nameof(PostgresCollectionB))]
public sealed class GenericRepositoryTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private GenericRepository<ApiKey, Guid> repository = null!;
    private Guid organizationId;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        repository = new GenericRepository<ApiKey, Guid>(context);

        Organization organization = Organization.Create(new OrganizationParams("Test Org", $"test-org-{Guid.NewGuid():N}"));
        context.Organizations.Add(organization);
        await context.SaveChangesAsync();
        organizationId = organization.Id;
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private ApiKey CreateApiKey(string name) =>
        ApiKey.Create(
            new ApiKeyParams(name), name.Length >= 8 ? name[..8] : name.PadRight(8, 'x'), "hash-" + name, organizationId);

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

    // Projected in SQL: only the selected columns come back, not whole entities.
    [Fact]
    public async Task ListAsync_WithSelector_ReturnsOnlyTheProjectionOfMatchingRows()
    {
        await SeedAsync(CreateApiKey("Active-1"), CreateApiKey("Active-2"));
        ApiKey inactive = CreateApiKey("Inactive-1");
        inactive.Deactivate();
        await SeedAsync(inactive);

        IReadOnlyList<string> names = await repository.ListAsync(k => k.IsActive, k => k.Name);

        Assert.Equal(["Active-1", "Active-2"], names.Order());
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
        ApiKey older = CreateApiKey("Older");
        ApiKey newer = CreateApiKey("Newer");
        await SeedAsync(older, newer);

        // SetAuditFields stamps CreatedAt = now on every Added entity, so both rows share a timestamp. Back-date
        // "older" via raw SQL (bypasses the change tracker) to give the ordering assertion a real difference.
        await context.Database.ExecuteSqlInterpolatedAsync(
            $"UPDATE api_keys SET created_at = {older.CreatedAt.AddHours(-1)} WHERE id = {older.Id}");

        (IReadOnlyList<ApiKey> items, int totalCount) = await repository.ListPagedAsync(k => true, 1, 10);

        Assert.Equal(2, totalCount);
        Assert.Equal("Newer", items[0].Name);
        Assert.Equal("Older", items[1].Name);
    }

    [Fact]
    public async Task ListPagedAsync_RowsShareCreatedAt_PagesAreDisjointAndTieBrokenById()
    {
        // One SeedAsync = one SaveChanges, so SetAuditFields stamps the same CreatedAt on all five.
        ApiKey[] keys = Enumerable.Range(1, 5).Select(i => CreateApiKey($"Key {i}")).ToArray();
        await SeedAsync(keys);

        // Postgres orders uuid by its canonical byte sequence, i.e. the lowercase hex string —
        // not by System.Guid.CompareTo, which compares the mixed-endian fields differently.
        List<Guid> expected = keys
            .Select(k => k.Id)
            .OrderByDescending(id => id.ToString(), StringComparer.Ordinal)
            .ToList();

        List<Guid> withPredicate = new();
        List<Guid> withSearch = new();
        for (int page = 1; page <= 3; page++)
        {
            (IReadOnlyList<ApiKey> a, _) = await repository.ListPagedAsync(_ => true, page, 2);
            (IReadOnlyList<ApiKey> b, _) = await repository.ListPagedAsync(_ => true, null, [k => k.Name], page, 2);
            withPredicate.AddRange(a.Select(k => k.Id));
            withSearch.AddRange(b.Select(k => k.Id));
        }

        Assert.Equal(expected, withPredicate);
        Assert.Equal(expected, withSearch);
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
