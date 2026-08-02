using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMemberConfiguration : IEntityTypeConfiguration<OrganizationMember>
{
    public void Configure(EntityTypeBuilder<OrganizationMember> builder)
    {
        builder.ToTable("organization_members");

        builder.HasKey(member => member.Id);

        builder.Property(member => member.Role)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(member => member.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        builder.HasIndex(member => new { member.OrganizationId, member.AccountId })
            .IsUnique();

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(member => member.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Account>()
            .WithMany()
            .HasForeignKey(member => member.AccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
