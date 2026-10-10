
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for EventBranch.
/// </summary>
/// <remarks>
/// Composite primary key on (EventId, BranchId).
///
/// Specification: §4 (Event Branch), §9.1.11, §15.8.9.
/// </remarks>
public class EventBranchConfiguration : IEntityTypeConfiguration<EventBranch>
{
    public void Configure(EntityTypeBuilder<EventBranch> builder)
    {
        builder.HasKey(e => new { e.EventId, e.BranchId });

        builder.HasOne(e => e.Event)
            .WithMany()
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
