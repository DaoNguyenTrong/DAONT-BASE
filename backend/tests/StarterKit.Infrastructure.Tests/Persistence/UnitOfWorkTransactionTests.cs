using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Domain.Entities;
using StarterKit.Infrastructure.Persistence;
using StarterKit.Infrastructure.Tests.TestSupport;

namespace StarterKit.Infrastructure.Tests.Persistence;

// Commits rows on purpose (that is what is under test), so it stays with the committing classes.
[Collection(nameof(PostgresCollection))]
public sealed class UnitOfWorkTransactionTests(PostgresContainerFixture fixture)
{
    private static Organization NewOrganization(string name) =>
        Organization.Create(new OrganizationParams(name, $"uow-{Guid.NewGuid():N}"));

    private async Task<bool> ExistsAsync(Guid id)
    {
        await using AppDbContext reader = fixture.CreateDbContext();
        return await reader.Organizations.AnyAsync(o => o.Id == id);
    }

    private static UnitOfWork CreateUnitOfWork(AppDbContext context) =>
        new(context, new ServiceCollection().BuildServiceProvider());

    [Fact]
    public async Task ExecuteInTransactionAsync_OperationSucceeds_CommitsEverySave()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        UnitOfWork unitOfWork = CreateUnitOfWork(context);
        Organization first = NewOrganization("First");
        Organization second = NewOrganization("Second");

        await unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            context.Organizations.Add(first);
            await unitOfWork.SaveChangesAsync(ct);
            context.Organizations.Add(second);
            await unitOfWork.SaveChangesAsync(ct);
        });

        Assert.True(await ExistsAsync(first.Id));
        Assert.True(await ExistsAsync(second.Id));
    }

    [Fact]
    public async Task ExecuteInTransactionAsync_OperationThrowsAfterASave_RollsThatSaveBack()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        UnitOfWork unitOfWork = CreateUnitOfWork(context);
        Organization saved = NewOrganization("Saved then undone");

        await Assert.ThrowsAsync<InvalidOperationException>(() => unitOfWork.ExecuteInTransactionAsync(async ct =>
        {
            context.Organizations.Add(saved);
            await unitOfWork.SaveChangesAsync(ct);
            throw new InvalidOperationException("second step failed");
        }));

        Assert.False(await ExistsAsync(saved.Id));
        Assert.Null(context.Database.CurrentTransaction);
    }

    // Postgres has no nested transactions: opening a second one would throw. Joining the caller's means the
    // caller still decides — here it rolls back, and the inner work goes with it.
    [Fact]
    public async Task ExecuteInTransactionAsync_TransactionAlreadyOpen_JoinsItInsteadOfCommitting()
    {
        await using AppDbContext context = fixture.CreateDbContext();
        UnitOfWork unitOfWork = CreateUnitOfWork(context);
        Organization inner = NewOrganization("Inner");

        await using (IDbContextTransaction outer = await context.Database.BeginTransactionAsync())
        {
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                context.Organizations.Add(inner);
                await unitOfWork.SaveChangesAsync(ct);
            });

            Assert.Same(outer, context.Database.CurrentTransaction);
            await outer.RollbackAsync();
        }

        Assert.False(await ExistsAsync(inner.Id));
    }
}
