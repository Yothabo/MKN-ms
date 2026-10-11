using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Duty.
/// </summary>
/// <remarks>
/// Carries no reference to Role. The relationship — when one exists —
/// is expressed by a DutyRule row.
///
/// Specification: §4 (Duty), §9.1.2.
/// </remarks>
public class DutyConfiguration : IEntityTypeConfiguration<Duty>
{
    public void Configure(EntityTypeBuilder<Duty> builder)
    {
        builder.HasKey(e => e.DutyId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
