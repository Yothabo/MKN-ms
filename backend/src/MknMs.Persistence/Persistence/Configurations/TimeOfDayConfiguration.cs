
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the TimeOfDay lookup.
/// </summary>
/// <remarks>
/// Specification: §4 (lookups), §9.1.12, §9.7.
/// </remarks>
public class TimeOfDayConfiguration : IEntityTypeConfiguration<TimeOfDay>
{
    public void Configure(EntityTypeBuilder<TimeOfDay> builder)
    {
        builder.HasKey(e => e.TimeOfDayId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
