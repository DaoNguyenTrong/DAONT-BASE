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
        var key = (typeof(T), typeof(TId));

        if (!repositories.TryGetValue(key, out var repository))
        {
            repository = serviceProvider.GetRequiredService<IRepository<T, TId>>();
            repositories[key] = repository;
        }

        return (IRepository<T, TId>)repository;
    }

    public IRepository<T> Repository<T>()
        where T : BaseEntity
    {
        var key = (typeof(T), typeof(int));

        if (!repositories.TryGetValue(key, out var repository))
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
}
