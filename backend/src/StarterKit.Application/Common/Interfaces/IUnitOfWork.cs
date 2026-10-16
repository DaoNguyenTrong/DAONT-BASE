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
    /// Runs <paramref name="operation"/> in one database transaction — for a business operation that needs
    /// <b>several</b> <see cref="SaveChangesAsync"/> calls in an explicit order. An exception from
    /// <paramref name="operation"/> rolls every one of them back.
    /// </summary>
    /// <remarks>
    /// Not "to be safe": one <see cref="SaveChangesAsync"/> is already atomic, so batch the changes into a single
    /// save whenever you can. What one save can't buy is <b>order</b> — EF sorts statements against the same table
    /// by key, not by code order, so two changes a unique index forbids from coexisting (swap two rows' unique
    /// values, retire the current row before inserting its replacement) can be emitted the wrong way round and fail
    /// halfway. Splitting them into separate saves fixes the order; the transaction keeps them one unit.
    /// If a transaction is already open (a caller's, or a test's rollback-only one) the operation joins it instead
    /// of opening another, and the outer owner decides commit or rollback. Joining takes no savepoint: a failed
    /// statement inside aborts the <b>whole</b> outer transaction (Postgres answers every later statement with
    /// 25P02), so a caller can't catch an inner failure and carry on — let it propagate.
    /// After a rollback the change tracker still holds what the earlier saves wrote, as if it were in the
    /// database. Don't retry on the same unit of work; discard the scope and start from a fresh one.
    /// Not compatible with a retrying execution strategy (<c>EnableRetryOnFailure</c>), which rejects a
    /// user-opened transaction — wrap the call in <c>CreateExecutionStrategy().ExecuteAsync</c> if one is enabled.
    /// </remarks>
    Task ExecuteInTransactionAsync(
        Func<CancellationToken, Task> operation,
        CancellationToken cancellationToken = default);
}
