using System.Linq.Expressions;
using NSubstitute;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Application.Tests.TestSupport;

internal static class RepositoryPredicateStub
{
    public static void StubFirstOrDefault<T>(IRepository<T, Guid> repo, IReadOnlyList<T> seed)
        where T : BaseEntity<Guid>
        => Stub(repo, seed);

    public static void StubFirstOrDefault<T>(IRepository<T, long> repo, IReadOnlyList<T> seed)
        where T : BaseEntity<long>
        => Stub(repo, seed);

    public static void StubFirstOrDefault<T>(IRepository<T, int> repo, IReadOnlyList<T> seed)
        where T : BaseEntity<int>
        => Stub(repo, seed);

    private static void Stub<T, TId>(IRepository<T, TId> repo, IReadOnlyList<T> seed)
        where T : BaseEntity<TId>
        where TId : notnull
    {
        repo.FirstOrDefaultAsync(Arg.Any<Expression<Func<T, bool>>>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                Expression<Func<T, bool>> predicate = callInfo.Arg<Expression<Func<T, bool>>>()!;
                return Task.FromResult(seed.AsQueryable().Where(predicate).FirstOrDefault());
            });
    }
}
