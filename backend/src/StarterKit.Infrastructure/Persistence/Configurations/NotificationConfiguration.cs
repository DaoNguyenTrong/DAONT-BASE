using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class NotificationConfiguration : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        builder.ToTable("notifications");

        builder.HasKey(n => n.Id);

        builder.Property(n => n.Type)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(n => n.Data)
            .HasColumnType("jsonb");

        builder.Property(n => n.CreatedAt)
            .IsRequired();

        builder.HasIndex(n => n.AccountId);

        builder.HasIndex(n => new { n.AccountId, n.ReadAt });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(n => n.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
