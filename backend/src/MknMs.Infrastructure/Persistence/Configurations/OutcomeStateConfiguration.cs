using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the OutcomeState lookup.
/// </summary>
/// <remarks>
/// Specification: §4 (lookups), §9.1.12, §12.1.3.
/// </remarks>
public class OutcomeStateConfiguration : IEntityTypeConfiguration<OutcomeState>
{
    public void Configure(EntityTypeBuilder<OutcomeState> builder)
    {
        builder.HasKey(e => e.OutcomeStateId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
    }
}
