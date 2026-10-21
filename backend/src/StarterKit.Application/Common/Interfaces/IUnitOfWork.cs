using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<T, TId> Repository<T, TId>()
        where T : BaseEntity<TId>
        where TId : notnull;

    IRepository<T> Repository<T>()
        where T : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Runs <paramref name="operation"/> in one transaction; a throw rolls it all back. Only for several
    /// <see cref="SaveChangesAsync"/> calls that must run in order — one save is already atomic. Joining an open
    /// transaction, rollback and retry rules: docs/persistence.md § Explicit transactions.
    /// </summary>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
