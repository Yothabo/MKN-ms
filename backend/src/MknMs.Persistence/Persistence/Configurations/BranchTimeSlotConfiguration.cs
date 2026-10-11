
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for BranchTimeSlot.
/// </summary>
/// <remarks>
/// Multiple slots per (BranchId, DayOfWeek) are permitted — no
/// uniqueness constraint on that pair. Each slot is identified by its
/// TimeSlotId.
///
/// Specification: §4 (Branch Time Slot), §9.1.3, §9.7.
/// </remarks>
public class BranchTimeSlotConfiguration : IEntityTypeConfiguration<BranchTimeSlot>
{
    public void Configure(EntityTypeBuilder<BranchTimeSlot> builder)
    {
        builder.HasKey(e => e.TimeSlotId);

        builder.Property(e => e.BranchId)
            .IsRequired();

        builder.Property(e => e.DayOfWeek)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.TimeOfDayId)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();

        builder.HasOne(e => e.Branch)
            .WithMany()
            .HasForeignKey(e => e.BranchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TimeOfDay)
            .WithMany()
            .HasForeignKey(e => e.TimeOfDayId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.BranchId)
            .HasDatabaseName("ix_branch_time_slot_branch");
    }
}
