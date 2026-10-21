using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Infrastructure.Persistence;

public sealed class UnitOfWork(
    AppDbContext dbContext,
    IServiceProvider serviceProvider) : IUnitOfWork
{
    private readonly Dictionary<(Type, Type), object> repositories = [];

    public IRepository<T, TId> Repository<T, TId>()
        where T : BaseEntity<TId>
        where TId : notnull
    {
        (Type, Type) key = (typeof(T), typeof(TId));

        if (!repositories.TryGetValue(key, out object? repository))
        {
            repository = serviceProvider.GetRequiredService<IRepository<T, TId>>();
            repositories[key] = repository;
        }

        return (IRepository<T, TId>)repository;
    }

    public IRepository<T> Repository<T>()
        where T : BaseEntity
    {
        (Type, Type) key = (typeof(T), typeof(int));

        if (!repositories.TryGetValue(key, out object? repository))
        {
            repository = serviceProvider.GetRequiredService<IRepository<T>>();
            repositories[key] = repository;
        }

        return (IRepository<T>)repository;
    }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default)
    {
        // Postgres can't nest transactions — join the open one and leave commit/rollback to its owner.
        if (dbContext.Database.CurrentTransaction is not null)
        {
            await operation(cancellationToken);
            return;
        }

        // Disposing without a commit (operation threw) rolls back.
        await using IDbContextTransaction transaction =
            await dbContext.Database.BeginTransactionAsync(cancellationToken);

        await operation(cancellationToken);

        await transaction.CommitAsync(cancellationToken);
    }
}
