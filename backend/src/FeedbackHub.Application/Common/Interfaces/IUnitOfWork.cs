using FeedbackHub.Domain.Entities;
using FeedbackHub.Domain.Interfaces;

namespace FeedbackHub.Application.Common.Interfaces;

public interface IUnitOfWork
{
    IRepository<T, TId> Repository<T, TId>()
        where T : BaseEntity<TId>
        where TId : notnull;

    IRepository<T> Repository<T>()
        where T : BaseEntity;

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
