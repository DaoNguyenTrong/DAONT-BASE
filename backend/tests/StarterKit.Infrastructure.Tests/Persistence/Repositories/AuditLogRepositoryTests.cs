using Microsoft.EntityFrameworkCore.Storage;
using StarterKit.Application.Services.AuditLogs;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Persistence.Repositories;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence.Repositories;

[Collection(nameof(PostgresCollection))]
public sealed class AuditLogRepositoryTests(PostgresContainerFixture fixture) : IAsyncLifetime
{
    private AppDbContext context = null!;
    private IDbContextTransaction transaction = null!;
    private AuditLogRepository repository = null!;

    public async Task InitializeAsync()
    {
        context = fixture.CreateDbContext();
        transaction = await context.Database.BeginTransactionAsync();
        repository = new AuditLogRepository(context);
    }

    // Never committed — disposing the transaction issues a ROLLBACK, keeping tests isolated.
    public async Task DisposeAsync()
    {
        await transaction.DisposeAsync();
        await context.DisposeAsync();
    }

    private static AuditLog CreateAuditLog(
        string entityName = "SomeEntity",
        string action = "Added",
        string? userId = null,
        DateTime? timestamp = null) =>
        new()
        {
            EntityName = entityName,
            EntityId = Guid.NewGuid().ToString(),
            Action = action,
            UserId = userId,
            Timestamp = timestamp ?? DateTime.UtcNow
        };

    private async Task SeedAsync(params AuditLog[] logs)
    {
        context.AuditLogs.AddRange(logs);
        await context.SaveChangesAsync();
    }

    // ListPagedAsync — search (ILike)

    [Fact]
    public async Task ListPagedAsync_SearchTerm_MatchesEntityNameCaseInsensitivePartial()
    {
        await SeedAsync(
            CreateAuditLog(entityName: "SearchableWidgetXyz"),
            CreateAuditLog(entityName: "UnrelatedThing"));

        (IReadOnlyList<AuditLogDto> items, int totalCount) = await repository.ListPagedAsync(
            1, 10, "searchablewidget", null, null);

        Assert.Equal(1, totalCount);
        Assert.Equal("SearchableWidgetXyz", items[0].EntityName);
    }

    [Fact]
    public async Task ListPagedAsync_SearchTerm_MatchesActionColumn()
    {
        await SeedAsync(
            CreateAuditLog(action: "UniqueActionMarker"),
            CreateAuditLog(action: "Deleted"));

        (IReadOnlyList<AuditLogDto> items, int totalCount) = await repository.ListPagedAsync(
            1, 10, "uniqueactionmarker", null, null);

        Assert.Equal(1, totalCount);
        Assert.Equal("UniqueActionMarker", items[0].Action);
    }

    [Fact]
    public async Task ListPagedAsync_NoSearchTerm_ReturnsAllMatchingOtherFilters()
    {
        AuditLog log = CreateAuditLog(entityName: "NoFilterEntity");
        await SeedAsync(log);

        (IReadOnlyList<AuditLogDto> items, int totalCount) = await repository.ListPagedAsync(
            1, 10, null, null, null);

        Assert.Contains(items, i => i.EntityName == "NoFilterEntity");
        Assert.True(totalCount >= 1);
    }

    // ListPagedAsync — userId filter

    [Fact]
    public async Task ListPagedAsync_UserIdFilter_ReturnsOnlyThatUsersLogs()
    {
        Guid userId = Guid.NewGuid();
        await SeedAsync(
            CreateAuditLog(entityName: "OwnedByUser", userId: userId.ToString()),
            CreateAuditLog(entityName: "OwnedByOther", userId: Guid.NewGuid().ToString()));

        (IReadOnlyList<AuditLogDto> items, int totalCount) = await repository.ListPagedAsync(
            1, 10, null, userId, null);

        Assert.Equal(1, totalCount);
        Assert.Equal("OwnedByUser", items[0].EntityName);
    }

    // ListPagedAsync — systemOnly filter

    [Fact]
    public async Task ListPagedAsync_SystemOnly_ReturnsOnlyNullUserIdLogs()
    {
        AuditLog systemLog = CreateAuditLog(entityName: "SystemGeneratedMarker", userId: null);
        AuditLog userLog = CreateAuditLog(entityName: "UserGeneratedMarker", userId: Guid.NewGuid().ToString());
        await SeedAsync(systemLog, userLog);

        (IReadOnlyList<AuditLogDto> items, int _) = await repository.ListPagedAsync(
            1, 50, "GeneratedMarker", null, true);

        Assert.Contains(items, i => i.EntityName == "SystemGeneratedMarker");
        Assert.DoesNotContain(items, i => i.EntityName == "UserGeneratedMarker");
        Assert.All(items, i => Assert.Null(i.UserId));
    }

    // ListPagedAsync — ordering and paging

    [Fact]
    public async Task ListPagedAsync_OrdersByTimestampDescending()
    {
        AuditLog older = CreateAuditLog(entityName: "OrderOlder", timestamp: DateTime.UtcNow.AddHours(-1));
        AuditLog newer = CreateAuditLog(entityName: "OrderNewer", timestamp: DateTime.UtcNow);
        await SeedAsync(older, newer);

        (IReadOnlyList<AuditLogDto> items, int _) = await repository.ListPagedAsync(
            1, 10, "Order", null, null);

        Assert.Equal("OrderNewer", items[0].EntityName);
        Assert.Equal("OrderOlder", items[1].EntityName);
    }

    // ListPagedAsync — left join to Accounts

    [Fact]
    public async Task ListPagedAsync_UserIdMatchesAccount_PopulatesUserName()
    {
        Account account = Account.Create(new AccountParams("Nguyen Van A", "nva-audit", "nva-audit@example.com"));
        context.Accounts.Add(account);
        await context.SaveChangesAsync();
        AuditLog log = CreateAuditLog(entityName: "JoinedToAccount", userId: account.Id.ToString());
        await SeedAsync(log);

        (IReadOnlyList<AuditLogDto> items, int _) = await repository.ListPagedAsync(
            1, 10, "JoinedToAccount", null, null);

        Assert.Equal("Nguyen Van A", items[0].UserName);
    }

    [Fact]
    public async Task ListPagedAsync_UserIdMatchesNoAccount_UserNameIsNull()
    {
        AuditLog log = CreateAuditLog(entityName: "OrphanedUserId", userId: Guid.NewGuid().ToString());
        await SeedAsync(log);

        (IReadOnlyList<AuditLogDto> items, int _) = await repository.ListPagedAsync(
            1, 10, "OrphanedUserId", null, null);

        Assert.Null(items[0].UserName);
    }

    // GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsDto()
    {
        AuditLog log = CreateAuditLog(entityName: "GetByIdTarget");
        await SeedAsync(log);

        AuditLogDto? result = await repository.GetByIdAsync(log.Id);

        Assert.NotNull(result);
        Assert.Equal("GetByIdTarget", result!.EntityName);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ReturnsNull()
    {
        AuditLogDto? result = await repository.GetByIdAsync(long.MaxValue);

        Assert.Null(result);
    }
}
