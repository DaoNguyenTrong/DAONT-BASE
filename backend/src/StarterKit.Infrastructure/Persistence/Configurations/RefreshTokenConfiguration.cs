using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class RefreshTokenConfiguration : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("refresh_tokens");

        builder.HasKey(token => token.Id);

        builder.Property(token => token.TokenHash)
            .HasMaxLength(64)
            .IsRequired();

        builder.Property(token => token.DeviceInfo)
            .HasMaxLength(500);

        builder.Property(token => token.IpAddress)
            .HasMaxLength(100);

        builder.Property(token => token.ExpiresAt)
            .IsRequired();

        builder.Property(token => token.IsPersistent)
            .IsRequired()
            .HasDefaultValue(false);

        builder.Property(token => token.CreatedAt)
            .IsRequired();

        builder.Property(token => token.LoginAt)
            .IsRequired();

        builder.Property(token => token.FamilyId)
            .IsRequired();

        builder.HasIndex(token => token.TokenHash)
            .IsUnique();

        builder.HasIndex(token => token.AccountId);

        // Reuse detection and concurrent-rotation lookups query the family scoped
        // to one account (see AuthService.RefreshTokenAsync).
        builder.HasIndex(token => new { token.AccountId, token.FamilyId });

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(token => token.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(token => token.OrganizationId)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
