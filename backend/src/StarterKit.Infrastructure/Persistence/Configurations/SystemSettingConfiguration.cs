using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using StarterKit.Domain.Entities;

namespace StarterKit.Infrastructure.Persistence.Configurations;

public sealed class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.ToTable("system_settings");

        builder.HasKey(setting => setting.Id);

        builder.Property(setting => setting.Key)
            .HasMaxLength(200)
            .IsRequired();

        builder.Property(setting => setting.Value)
            .HasColumnType("text");

        builder.Property(setting => setting.CreatedAt)
            .IsRequired();

        builder.HasIndex(setting => setting.Key)
            .IsUnique();
    }
}
