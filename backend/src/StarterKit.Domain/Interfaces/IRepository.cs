using System.Linq.Expressions;
using StarterKit.Domain.Entities;

namespace StarterKit.Domain.Interfaces;

public interface IRepository<T, TId>
    where T : BaseEntity<TId>
    where TId : notnull
{
    Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default);

    Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
        Expression<Func<T, bool>> predicate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
        Expression<Func<T, bool>> predicate,
        string? searchTerm,
        Expression<Func<T, string?>>[] searchColumns,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    Task AddAsync(T entity, CancellationToken cancellationToken = default);

    void Update(T entity);

    void Delete(T entity);
}

public interface IRepository<T> : IRepository<T, int>
    where T : BaseEntity
{
}
