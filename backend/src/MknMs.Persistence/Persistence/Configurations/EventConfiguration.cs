
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Event.
/// </summary>
/// <remarks>
/// HostBranchId is non-null as of Step 7. Location is retained until
/// Step 8.
///
/// Specification: §4 (Event), §9.1.11, §9.6, §9.7, §15.8.8,
/// §15.8.20.
/// </remarks>
public class EventConfiguration : IEntityTypeConfiguration<Event>
{
    public void Configure(EntityTypeBuilder<Event> builder)
    {
        builder.HasKey(e => e.EventId);

        builder.Property(e => e.Name).IsRequired().HasMaxLength(200);
        builder.Property(e => e.StartDate).IsRequired();
        builder.Property(e => e.EndDate).IsRequired();
        builder.Property(e => e.Type).IsRequired().HasMaxLength(100);
        builder.Property(e => e.HostBranchId).IsRequired();
        builder.Property(e => e.UsesAttendanceRegister);
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();

        builder.HasOne(e => e.HostBranch)
            .WithMany()
            .HasForeignKey(e => e.HostBranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.HostBranchId)
            .HasDatabaseName("ix_event_host_branch");
    }
}
