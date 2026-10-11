
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the OutcomeState lookup.
/// </summary>
/// <remarks>
/// Specification: §4 (lookups), §9.1.12, §9.7, §12.1.3.
/// </remarks>
public class OutcomeStateConfiguration : IEntityTypeConfiguration<OutcomeState>
{
    public void Configure(EntityTypeBuilder<OutcomeState> builder)
    {
        builder.HasKey(e => e.OutcomeStateId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
