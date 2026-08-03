using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using StarterKit.Application.Common.Interfaces;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence;

public sealed class AppDbContext(
    DbContextOptions<AppDbContext> options,
    IDateTimeProvider? dateTimeProvider = null,
    ICurrentUserService? currentUserService = null,
    IHttpContextAccessor? httpContextAccessor = null)
    : DbContext(options), IDataProtectionKeyContext
{
    private static readonly HashSet<Type> AuditExcludedEntityTypes =
    [
        typeof(RefreshToken),
        typeof(EmailVerificationToken)
    ];

    public DbSet<Account> Accounts => Set<Account>();

    public DbSet<ApiKey> ApiKeys => Set<ApiKey>();

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>()!;

    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();

    public DbSet<ExternalLogin> ExternalLogins => Set<ExternalLogin>();

    public DbSet<Organization> Organizations => Set<Organization>();

    public DbSet<OrganizationMember> OrganizationMembers => Set<OrganizationMember>();

    public DbSet<OrganizationMemberRole> OrganizationMemberRoles => Set<OrganizationMemberRole>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<StoredFile> StoredFiles => Set<StoredFile>();

    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        var now = dateTimeProvider?.UtcNow ?? DateTime.UtcNow;
        var userId = currentUserService?.UserId;

        SetAuditFields(now, userId);

        var auditEntries = CaptureAuditEntries(now, userId);

        var result = await base.SaveChangesAsync(cancellationToken);

        await SaveAuditLogsAsync(auditEntries, now, cancellationToken);

        return result;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);

        base.OnModelCreating(modelBuilder);
    }

    private void SetAuditFields(DateTime now, string? userId)
    {
        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is not IHasAuditFields auditableEntity)
            {
                continue;
            }

            if (entry.State == EntityState.Added)
            {
                auditableEntity.CreatedAt = now;
                auditableEntity.CreatedBy = userId;
            }

            if (entry.State == EntityState.Modified)
            {
                auditableEntity.UpdatedAt = now;
                auditableEntity.UpdatedBy = userId;
            }
        }
    }

    private List<AuditEntry> CaptureAuditEntries(DateTime now, string? userId)
    {
        var entries = new List<AuditEntry>();
        var httpContext = httpContextAccessor?.HttpContext;

        foreach (var entry in ChangeTracker.Entries())
        {
            if (entry.Entity is AuditLog ||
                entry.Entity is not IHasAuditFields ||
                AuditExcludedEntityTypes.Contains(entry.Entity.GetType()))
            {
                continue;
            }

            if (entry.State is not (EntityState.Added or EntityState.Modified or EntityState.Deleted))
            {
                continue;
            }

            var auditEntry = new AuditEntry(entry)
            {
                EntityName = entry.Entity.GetType().Name,
                Action = entry.State.ToString(),
                UserId = userId,
                IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = httpContext?.Request.Headers.UserAgent.ToString()
            };

            foreach (var property in entry.Properties)
            {
                if (property.IsTemporary)
                {
                    auditEntry.TemporaryProperties.Add(property);
                    continue;
                }

                var propertyName = property.Metadata.Name;

                if (entry.State == EntityState.Added)
                {
                    auditEntry.NewValues[propertyName] = property.CurrentValue;
                }
                else if (entry.State == EntityState.Deleted)
                {
                    auditEntry.OldValues[propertyName] = property.OriginalValue;
                }
                else if (property.IsModified)
                {
                    auditEntry.OldValues[propertyName] = property.OriginalValue;
                    auditEntry.NewValues[propertyName] = property.CurrentValue;
                }
            }

            entries.Add(auditEntry);
        }

        return entries;
    }

    private async Task SaveAuditLogsAsync(
        List<AuditEntry> auditEntries,
        DateTime timestamp,
        CancellationToken cancellationToken)
    {
        if (auditEntries.Count == 0)
        {
            return;
        }

        foreach (var auditEntry in auditEntries)
        {
            foreach (var prop in auditEntry.TemporaryProperties)
            {
                if (prop.Metadata.Name == "Id")
                {
                    auditEntry.NewValues["Id"] = prop.CurrentValue;
                }
            }

            AuditLogs.Add(auditEntry.ToAuditLog(timestamp));
        }

        await base.SaveChangesAsync(cancellationToken);
    }
}
