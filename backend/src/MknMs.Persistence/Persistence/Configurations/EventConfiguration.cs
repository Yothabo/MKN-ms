using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Event.
/// </summary>
/// <remarks>
/// Type and Location are free text. May span multiple days.
///
/// Specification: §4 (Event), §9.1.11.
/// </remarks>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.EndDate).IsRequired();
        builder.Property(e => e.Location).IsRequired().HasMaxLength(500);
        builder.Property(e => e.Type).IsRequired().HasMaxLength(100);
        builder.Property(e => e.IsActive).IsRequired();
    }
}
