using Microsoft.EntityFrameworkCore;
using FeedbackHub.Application.Services.AuditLogs;

namespace FeedbackHub.Infrastructure.Persistence.Repositories;

public sealed class AuditLogRepository(AppDbContext dbContext) : IAuditLogRepository
{
    public async Task<(IReadOnlyList<AuditLogDto> Items, int TotalCount)> ListPagedAsync(
        int pageNumber,
        int pageSize,
        string? search,
        Guid? userId,
        bool? systemOnly,
        CancellationToken cancellationToken = default)
    {
        IQueryable<AuditLog> query = dbContext.AuditLogs.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(search))
        {
            string pattern = $"%{search}%";
            query = query.Where(log =>
                EF.Functions.ILike(log.EntityName, pattern) ||
                EF.Functions.ILike(log.EntityId, pattern) ||
                EF.Functions.ILike(log.Action, pattern) ||
                (log.UserId != null && EF.Functions.ILike(log.UserId, pattern)));
        }

        if (userId is not null)
        {
            string userIdText = userId.Value.ToString();
            query = query.Where(log => log.UserId == userIdText);
        }

        if (systemOnly == true)
        {
            query = query.Where(log => log.UserId == null);
        }

        int totalCount = await query.CountAsync(cancellationToken);

        IReadOnlyList<AuditLogDto> items = await (
            from log in query
            join account in dbContext.Accounts.AsNoTracking()
                on log.UserId equals account.Id.ToString() into accountGroup
            from account in accountGroup.DefaultIfEmpty()
            orderby log.Timestamp descending
            select new AuditLogDto(
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.OldValues,
                log.NewValues,
                log.UserId,
                account != null ? account.Name : null,
                log.IpAddress,
                log.UserAgent,
                log.Timestamp))
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return (items, totalCount);
    }

    public Task<AuditLogDto?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return (
            from log in dbContext.AuditLogs.AsNoTracking()
            join account in dbContext.Accounts.AsNoTracking()
                on log.UserId equals account.Id.ToString() into accountGroup
            from account in accountGroup.DefaultIfEmpty()
            where log.Id == id
            select new AuditLogDto(
                log.Id,
                log.EntityName,
                log.EntityId,
                log.Action,
                log.OldValues,
                log.NewValues,
                log.UserId,
                account != null ? account.Name : null,
                log.IpAddress,
                log.UserAgent,
                log.Timestamp))
            .FirstOrDefaultAsync(cancellationToken);
    }
}
