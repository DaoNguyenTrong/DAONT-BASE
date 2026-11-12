using NSubstitute;
using StarterKit.Application.Common.Interfaces;
//#if (OrgScoped)
using StarterKit.Application.Resources;
//#endif
using StarterKit.Application.Services.Samples;
using StarterKit.Application.Tests.TestSupport;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Exceptions;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.Services.Samples;

public class SampleServiceTests
{
//#if (OrgScoped)
    private static readonly Guid OrganizationId = Guid.NewGuid();

//#endif
    private sealed record Fixture(SampleService Service, IRepository<Sample, Guid> Repo, IUnitOfWork UnitOfWork);

//#if (OrgScoped)
    private static Fixture CreateFixture(bool signedIn = true)
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Sample, Guid> repo = Substitute.For<IRepository<Sample, Guid>>();
        unitOfWork.Repository<Sample, Guid>().Returns(repo);
        ICurrentTenantProvider currentTenantProvider = Substitute.For<ICurrentTenantProvider>();
        currentTenantProvider.OrganizationId.Returns(signedIn ? OrganizationId : null);

        return new Fixture(new SampleService(unitOfWork, currentTenantProvider), repo, unitOfWork);
    }

    private static Sample CreateEntity(string name = "First", Guid? organizationId = null)
    {
        Sample entity = Sample.Create(new SampleParams(name), organizationId ?? OrganizationId);
        entity.CreatedAt = DateTime.UtcNow;
        return entity;
    }

    private static void Seed(Fixture f, params Sample[] rows)
    {
        RepositoryPredicateStub.StubFirstOrDefault(f.Repo, rows);
        RepositoryPredicateStub.StubListAsync(f.Repo, rows);
    }
//#else
    private static Fixture CreateFixture()
    {
        IUnitOfWork unitOfWork = Substitute.For<IUnitOfWork>();
        IRepository<Sample, Guid> repo = Substitute.For<IRepository<Sample, Guid>>();
        unitOfWork.Repository<Sample, Guid>().Returns(repo);

        return new Fixture(new SampleService(unitOfWork), repo, unitOfWork);
    }

    private static Sample CreateEntity(string name = "First")
    {
        Sample entity = Sample.Create(new SampleParams(name));
        entity.CreatedAt = DateTime.UtcNow;
        return entity;
    }

    private static void Seed(Fixture f, params Sample[] rows)
    {
        f.Repo.GetByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(call => rows.FirstOrDefault(row => row.Id == call.Arg<Guid>()));
        f.Repo.ListAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Sample>)rows);
    }
//#endif

    // CreateAsync

    [Fact]
    public async Task CreateAsync_ValidRequest_PersistsAndReturnsDto()
    {
        Fixture f = CreateFixture();

        SampleDto result = await f.Service.CreateAsync(new SampleRequest("First"), CancellationToken.None);

        Assert.Equal("First", result.Name);
        await f.Repo.Received(1).AddAsync(Arg.Is<Sample>(e => e != null && e.Name == "First"), Arg.Any<CancellationToken>());
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_BlankName_ThrowsAndDoesNotPersist()
    {
        Fixture f = CreateFixture();

        await ApplicationAssert.ThrowsWithMessageAsync<DomainException>(
            DomainMessages.SampleNameRequired,
            () => f.Service.CreateAsync(new SampleRequest(" "), CancellationToken.None));

        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
//#if (OrgScoped)

    [Fact]
    public async Task CreateAsync_NoActiveOrganization_ThrowsForbidden()
    {
        Fixture f = CreateFixture(signedIn: false);

        await ApplicationAssert.ThrowsWithMessageAsync<ForbiddenException>(
            ApplicationMessages.OrganizationAccessDenied,
            () => f.Service.CreateAsync(new SampleRequest("First"), CancellationToken.None));
    }
//#endif

    // GetAllAsync

    [Fact]
    public async Task GetAllAsync_TwoRows_OrdersByCreatedAtDescending()
    {
        Fixture f = CreateFixture();
        Sample older = CreateEntity("Older");
        older.CreatedAt = DateTime.UtcNow.AddHours(-1);
        Sample newer = CreateEntity("Newer");
        Seed(f, older, newer);

        IReadOnlyList<SampleDto> result = await f.Service.GetAllAsync(CancellationToken.None);

        Assert.Equal(["Newer", "Older"], result.Select(d => d.Name));
    }
//#if (OrgScoped)

    [Fact]
    public async Task GetAllAsync_OtherOrganizationsRows_AreNotReturned()
    {
        Fixture f = CreateFixture();
        Seed(f, CreateEntity("Mine"), CreateEntity("Theirs", Guid.NewGuid()));

        IReadOnlyList<SampleDto> result = await f.Service.GetAllAsync(CancellationToken.None);

        Assert.Equal("Mine", Assert.Single(result).Name);
    }
//#endif

    // GetAsync

    [Fact]
    public async Task GetAsync_Found_ReturnsDto()
    {
        Fixture f = CreateFixture();
        Sample entity = CreateEntity();
        Seed(f, entity);

        SampleDto result = await f.Service.GetAsync(entity.Id, CancellationToken.None);

        Assert.Equal(entity.Id, result.Id);
    }

    [Fact]
    public async Task GetAsync_Unknown_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Seed(f);
        Guid id = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Sample>(id, () => f.Service.GetAsync(id, CancellationToken.None));
    }
//#if (OrgScoped)

    [Fact]
    public async Task GetAsync_OtherOrganizationsRow_ThrowsNotFound()
    {
        Fixture f = CreateFixture();
        Sample theirs = CreateEntity("Theirs", Guid.NewGuid());
        Seed(f, theirs);

        await ApplicationAssert.AssertNotFoundAsync<Sample>(
            theirs.Id, () => f.Service.GetAsync(theirs.Id, CancellationToken.None));
    }
//#endif

    // UpdateAsync

    [Fact]
    public async Task UpdateAsync_Found_RenamesAndSaves()
    {
        Fixture f = CreateFixture();
        Sample entity = CreateEntity();
        Seed(f, entity);

        SampleDto result = await f.Service.UpdateAsync(entity.Id, new SampleRequest("Renamed"), CancellationToken.None);

        Assert.Equal("Renamed", result.Name);
        f.Repo.Received(1).Update(entity);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }
//#if (OrgScoped)

    [Fact]
    public async Task UpdateAsync_OtherOrganizationsRow_ThrowsNotFoundAndDoesNotSave()
    {
        Fixture f = CreateFixture();
        Sample theirs = CreateEntity("Theirs", Guid.NewGuid());
        Seed(f, theirs);

        await ApplicationAssert.AssertNotFoundAsync<Sample>(
            theirs.Id, () => f.Service.UpdateAsync(theirs.Id, new SampleRequest("Taken"), CancellationToken.None));

        Assert.Equal("Theirs", theirs.Name);
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
//#endif

    // DeleteAsync

    [Fact]
    public async Task DeleteAsync_Found_DeletesAndSaves()
    {
        Fixture f = CreateFixture();
        Sample entity = CreateEntity();
        Seed(f, entity);

        await f.Service.DeleteAsync(entity.Id, CancellationToken.None);

        f.Repo.Received(1).Delete(entity);
        await f.UnitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_Unknown_ThrowsNotFoundAndDoesNotDelete()
    {
        Fixture f = CreateFixture();
        Seed(f);
        Guid id = Guid.NewGuid();

        await ApplicationAssert.AssertNotFoundAsync<Sample>(id, () => f.Service.DeleteAsync(id, CancellationToken.None));

        f.Repo.DidNotReceive().Delete(Arg.Any<Sample>());
    }
//#if (OrgScoped)

    [Fact]
    public async Task DeleteAsync_OtherOrganizationsRow_ThrowsNotFoundAndDoesNotSave()
    {
        Fixture f = CreateFixture();
        Sample theirs = CreateEntity("Theirs", Guid.NewGuid());
        Seed(f, theirs);

        await ApplicationAssert.AssertNotFoundAsync<Sample>(
            theirs.Id, () => f.Service.DeleteAsync(theirs.Id, CancellationToken.None));

        f.Repo.DidNotReceive().Delete(Arg.Any<Sample>());
        await f.UnitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }
//#endif
}
