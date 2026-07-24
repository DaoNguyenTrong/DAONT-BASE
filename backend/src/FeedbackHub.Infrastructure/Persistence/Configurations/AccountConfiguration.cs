using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Configurations;

public sealed class AccountConfiguration : IEntityTypeConfiguration<Account>
{
    public void Configure(EntityTypeBuilder<Account> builder)
    {
        builder.ToTable("accounts");

        builder.HasKey(account => account.Id);

        builder.Property(account => account.Name)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(account => account.Phone)
            .HasMaxLength(20);

        builder.Property(account => account.Position)
            .HasMaxLength(100);

        builder.Property(account => account.Address)
            .HasMaxLength(500);

        builder.Property(account => account.Status)
            .HasDefaultValue(true)
            .IsRequired();

        builder.Property(account => account.Username)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(account => account.Email)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(account => account.PasswordHash)
            .HasMaxLength(200);

        builder.Property(account => account.EmailConfirmed)
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(account => account.CreatedAt)
            .IsRequired();

        builder.HasIndex(account => account.Username)
            .IsUnique();

        builder.HasIndex(account => account.Email)
            .IsUnique();
    }
}
