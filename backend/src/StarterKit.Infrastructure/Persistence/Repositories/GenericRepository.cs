using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using StarterKit.Domain.Entities;
using StarterKit.Domain.Interfaces;

namespace StarterKit.Infrastructure.Persistence.Repositories;

public class GenericRepository<T, TId>(AppDbContext dbContext) : IRepository<T, TId>
    where T : BaseEntity<TId>
    where TId : notnull
{
    private readonly DbSet<T> dbSet = dbContext.Set<T>();

    private static readonly MethodInfo ILikeMethod =
        typeof(NpgsqlDbFunctionsExtensions).GetMethod(
            nameof(NpgsqlDbFunctionsExtensions.ILike),
            BindingFlags.Public | BindingFlags.Static,
            [typeof(DbFunctions), typeof(string), typeof(string)])!;

    public Task<T?> GetByIdAsync(TId id, CancellationToken cancellationToken = default)
    {
        return dbSet.FirstOrDefaultAsync(entity => entity.Id.Equals(id), cancellationToken);
    }

    public Task<T?> FirstOrDefaultAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return dbSet.FirstOrDefaultAsync(predicate, cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await dbSet.ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<T>> ListAsync(
        Expression<Func<T, bool>> predicate,
        CancellationToken cancellationToken = default)
    {
        return await dbSet.Where(predicate).ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbSet.OrderBy(entity => entity.Id);
        int totalCount = await query.CountAsync(cancellationToken);
        List<T> items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
        Expression<Func<T, bool>> predicate,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbSet.Where(predicate);
        int totalCount = await query.CountAsync(cancellationToken);

        query = query.OrderByDescending(entity => entity.CreatedAt);

        List<T> items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public async Task<(IReadOnlyList<T> Items, int TotalCount)> ListPagedAsync(
        Expression<Func<T, bool>> predicate,
        string? searchTerm,
        Expression<Func<T, string?>>[] searchColumns,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        IQueryable<T> query = dbSet.Where(predicate);

        if (!string.IsNullOrEmpty(searchTerm) && searchColumns.Length > 0)
            query = query.Where(BuildILikePredicate($"%{searchTerm}%", searchColumns));

        int totalCount = await query.CountAsync(cancellationToken);
        List<T> items = await query
            .OrderByDescending(entity => entity.CreatedAt)
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    private static Expression<Func<T, bool>> BuildILikePredicate(
        string pattern,
        Expression<Func<T, string?>>[] selectors)
    {
        ParameterExpression param = Expression.Parameter(typeof(T), "e");
        MemberExpression efFunctions = Expression.Property(null, typeof(EF), nameof(EF.Functions));
        ConstantExpression patternExpr = Expression.Constant(pattern);

        Expression? body = null;
        foreach (Expression<Func<T, string?>> selector in selectors)
        {
            Expression column = ParameterReplacer.Replace(selector.Body, selector.Parameters[0], param);
            MethodCallExpression call = Expression.Call(null, ILikeMethod, efFunctions, column, patternExpr);
            body = body is null ? (Expression)call : Expression.OrElse(body, call);
        }

        return Expression.Lambda<Func<T, bool>>(body!, param);
    }

    private sealed class ParameterReplacer(ParameterExpression source, ParameterExpression target)
        : ExpressionVisitor
    {
        public static Expression Replace(Expression body, ParameterExpression source, ParameterExpression target)
            => new ParameterReplacer(source, target).Visit(body);

        protected override Expression VisitParameter(ParameterExpression node)
            => node == source ? target : base.VisitParameter(node);
    }

    public Task AddAsync(T entity, CancellationToken cancellationToken = default)
    {
        return dbSet.AddAsync(entity, cancellationToken).AsTask();
    }

    public void Update(T entity)
    {
        dbSet.Update(entity);
    }

    public void Delete(T entity)
    {
        dbSet.Remove(entity);
    }
}

public sealed class GenericRepository<T>(AppDbContext dbContext)
    : GenericRepository<T, int>(dbContext), IRepository<T>
    where T : BaseEntity
{
}
