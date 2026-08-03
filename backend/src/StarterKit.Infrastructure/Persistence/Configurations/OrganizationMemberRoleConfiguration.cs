using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class OrganizationMemberRoleConfiguration : IEntityTypeConfiguration<OrganizationMemberRole>
{
    public void Configure(EntityTypeBuilder<OrganizationMemberRole> builder)
    {
        builder.ToTable("organization_member_roles");

        builder.HasKey(memberRole => memberRole.Id);

        builder.HasIndex(memberRole => new { memberRole.OrganizationMemberId, memberRole.RoleId })
            .IsUnique();

        builder.HasOne<OrganizationMember>()
            .WithMany()
            .HasForeignKey(memberRole => memberRole.OrganizationMemberId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(memberRole => memberRole.RoleId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
