using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for SystemSetting.
/// </summary>
/// <remarks>
/// Key is the primary key. Required is a boolean with no default — the
/// administrator sets it when defining the setting. Value is optional
/// at the schema level; a required setting with no value causes the
/// consuming process to refuse to run.
///
/// Specification: §4 (System Setting), §15.3.
/// </remarks>
public class SystemSettingConfiguration : IEntityTypeConfiguration<SystemSetting>
{
    public void Configure(EntityTypeBuilder<SystemSetting> builder)
    {
        builder.HasKey(e => e.Key);

        builder.Property(e => e.Key).IsRequired().HasMaxLength(200);
        builder.Property(e => e.Value);
        builder.Property(e => e.Required).IsRequired();
        builder.Property(e => e.Description).HasMaxLength(1000);
    }
}
