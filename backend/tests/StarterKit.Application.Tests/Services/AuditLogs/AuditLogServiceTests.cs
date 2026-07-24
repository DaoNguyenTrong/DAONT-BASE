using NSubstitute;
using StarterKit.Application.Common.Models;
using StarterKit.Application.Services.AuditLogs;
using StarterKit.Application.Tests.TestSupport;

namespace StarterKit.Application.Tests.Services.AuditLogs;

public class AuditLogServiceTests
{
    private sealed record Fixture(AuditLogService Service, IAuditLogRepository Repository);

    private static Fixture CreateFixture()
    {
        IAuditLogRepository repository = Substitute.For<IAuditLogRepository>();
        AuditLogService service = new(repository);

        return new Fixture(service, repository);
    }

    private static AuditLogDto CreateDto(long id = 1) =>
        new(id, "Account", Guid.NewGuid().ToString(), "Create", null, null, null, null, null, null, DateTime.UtcNow);

    // GetAllAsync

    [Theory]
    [InlineData(0, 0, 1, 10)]
    [InlineData(-1, -1, 1, 10)]
    [InlineData(3, 50, 3, 50)]
    public async Task GetAllAsync_DefaultsInvalidPageValues(
        int requestPage, int requestSize, int expectedPage, int expectedSize)
    {
        Fixture f = CreateFixture();
        f.Repository.ListPagedAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<AuditLogDto>)[], 0));

        await f.Service.GetAllAsync(new PaginationRequest(requestPage, requestSize), null, null, CancellationToken.None);

        await f.Repository.Received(1).ListPagedAsync(
            expectedPage, expectedSize, Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_TrimsSearchTerm_AndPassesUserIdAndSystemOnly()
    {
        Fixture f = CreateFixture();
        Guid userId = Guid.NewGuid();
        f.Repository.ListPagedAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<AuditLogDto>)[], 0));

        await f.Service.GetAllAsync(
            new PaginationRequest(1, 10, "  create  "), userId, true, CancellationToken.None);

        await f.Repository.Received(1).ListPagedAsync(1, 10, "create", userId, true, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetAllAsync_WrapsRepositoryResultInPagedResult()
    {
        Fixture f = CreateFixture();
        AuditLogDto dto = CreateDto();
        f.Repository.ListPagedAsync(
                Arg.Any<int>(), Arg.Any<int>(), Arg.Any<string?>(), Arg.Any<Guid?>(), Arg.Any<bool?>(), Arg.Any<CancellationToken>())
            .Returns(((IReadOnlyList<AuditLogDto>)[dto], 1));

        PagedResult<AuditLogDto> result = await f.Service.GetAllAsync(
            new PaginationRequest(1, 10), null, null, CancellationToken.None);

        Assert.Single(result.Items);
        Assert.Equal(dto.Id, result.Items[0].Id);
        Assert.Equal(1, result.TotalCount);
    }

    // GetByIdAsync

    [Fact]
    public async Task GetByIdAsync_Found_ReturnsDto()
    {
        Fixture f = CreateFixture();
        AuditLogDto dto = CreateDto(42);
        f.Repository.GetByIdAsync(42, Arg.Any<CancellationToken>()).Returns(dto);

        AuditLogDto result = await f.Service.GetByIdAsync(42, CancellationToken.None);

        Assert.Equal(42, result.Id);
    }

    [Fact]
    public async Task GetByIdAsync_NotFound_ThrowsNotFoundWithAuditLogEntityName()
    {
        Fixture f = CreateFixture();
        f.Repository.GetByIdAsync(99, Arg.Any<CancellationToken>()).Returns((AuditLogDto?)null);

        await ApplicationAssert.AssertNotFoundAsync(
            "AuditLog", 99L, () => f.Service.GetByIdAsync(99, CancellationToken.None));
    }
}
