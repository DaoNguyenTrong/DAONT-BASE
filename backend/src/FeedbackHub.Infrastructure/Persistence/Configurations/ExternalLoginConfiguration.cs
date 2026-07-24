using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using FeedbackHub.Domain.Entities;

namespace FeedbackHub.Infrastructure.Persistence.Configurations;

public sealed class ExternalLoginConfiguration : IEntityTypeConfiguration<ExternalLogin>
{
    public void Configure(EntityTypeBuilder<ExternalLogin> builder)
    {
        builder.ToTable("external_logins");

        builder.HasKey(login => login.Id);

        builder.Property(login => login.Id)
            .HasColumnName("id");

        builder.Property(login => login.AccountId)
            .HasColumnName("account_id")
            .IsRequired();

        builder.Property(login => login.Provider)
            .HasColumnName("provider")
            .HasMaxLength(50)
            .IsRequired();

        builder.Property(login => login.ProviderUserId)
            .HasColumnName("provider_user_id")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(login => login.Email)
            .HasColumnName("email")
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(login => login.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(login => login.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(login => login.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(login => login.UpdatedBy)
            .HasColumnName("updated_by");

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(login => login.AccountId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(login => new { login.Provider, login.ProviderUserId })
            .IsUnique();

        builder.HasIndex(login => login.AccountId);
    }
}
