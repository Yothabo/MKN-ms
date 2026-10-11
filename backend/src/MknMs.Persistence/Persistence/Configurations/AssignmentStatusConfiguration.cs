
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the AssignmentStatus lookup.
/// </summary>
/// <remarks>
/// IsTerminal is a required boolean (amendment §15.1.2). It distinguishes
/// statuses that occupy a required slot from statuses that leave the slot
/// vacant. It is a capacity concept, not a lifecycle-impermanence concept.
///
/// Specification: §4 (lookups), §9.7, §11.1.2, §15.1.2.
/// </remarks>
public class AssignmentStatusConfiguration : IEntityTypeConfiguration<AssignmentStatus>
{
    public void Configure(EntityTypeBuilder<AssignmentStatus> builder)
    {
        builder.HasKey(e => e.AssignmentStatusId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsTerminal)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
