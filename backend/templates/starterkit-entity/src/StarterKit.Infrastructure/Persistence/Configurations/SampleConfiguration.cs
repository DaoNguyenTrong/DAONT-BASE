using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class SampleConfiguration : IEntityTypeConfiguration<Sample>
{
    public void Configure(EntityTypeBuilder<Sample> builder)
    {
        builder.ToTable("sample_rows");

        builder.HasKey(entity => entity.Id);

        builder.Property(entity => entity.Id)
            .HasColumnName("id");

        builder.Property(entity => entity.Name)
            .HasColumnName("name")
            .HasMaxLength(Sample.NameMaxLength)
            .IsRequired();

        builder.Property(entity => entity.CreatedAt)
            .HasColumnName("created_at")
            .IsRequired();

        builder.Property(entity => entity.UpdatedAt)
            .HasColumnName("updated_at");

        builder.Property(entity => entity.CreatedBy)
            .HasColumnName("created_by");

        builder.Property(entity => entity.UpdatedBy)
            .HasColumnName("updated_by");
//#if (OrgScoped)

        builder.Property(entity => entity.OrganizationId)
            .HasColumnName("organization_id")
            .IsRequired();

        builder.HasIndex(entity => entity.OrganizationId);

        builder.HasOne<Organization>()
            .WithMany()
            .HasForeignKey(entity => entity.OrganizationId)
            .OnDelete(DeleteBehavior.Cascade);
//#endif
    }
}
