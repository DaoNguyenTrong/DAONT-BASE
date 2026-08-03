using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.ToTable("roles");

        builder.HasKey(role => role.Id);

        builder.Property(role => role.Name)
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(role => role.SystemRoleKind)
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.HasIndex(role => new { role.OrganizationId, role.Name })
            .IsUnique();

        builder.HasIndex(role => new { role.OrganizationId, role.SystemRoleKind })
            .IsUnique()
            .HasFilter("\"SystemRoleKind\" IS NOT NULL");

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(role => role.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
