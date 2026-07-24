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
}
