using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for ServiceSchedule.
/// </summary>
/// <remarks>
/// StartTime lives here — not on ServiceDefinition, not on
/// BranchTimeSlot — because it varies per branch, per slot, and per
/// service simultaneously.
///
/// Active-row uniqueness constraint (amendment §15.2.3): an active
/// ServiceSchedule is uniquely identified by the pair
/// (ServiceDefId, TimeSlotId). Inactive schedules are excluded from the
/// constraint, so a deactivated schedule may be replaced.
///
/// Specification: §4 (Service Schedule), §9.1.5, §15.2.3.
/// </remarks>
public class ServiceScheduleConfiguration : IEntityTypeConfiguration<ServiceSchedule>
{
    public void Configure(EntityTypeBuilder<ServiceSchedule> builder)
    {
        builder.HasKey(e => e.ScheduleId);

        builder.Property(e => e.ServiceDefId)
            .IsRequired();

        builder.Property(e => e.TimeSlotId)
            .IsRequired();

        builder.Property(e => e.StartTime)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.HasOne(e => e.ServiceDefinition)
            .WithMany()
            .HasForeignKey(e => e.ServiceDefId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.TimeSlot)
            .WithMany()
            .HasForeignKey(e => e.TimeSlotId)
            .OnDelete(DeleteBehavior.Restrict);

        // Active-row uniqueness: (ServiceDefId, TimeSlotId) unique among
        // rows where is_active is TRUE. Expressed as a partial unique
        // index in PostgreSQL.
        builder.HasIndex(e => new { e.ServiceDefId, e.TimeSlotId })
            .IsUnique()
            .HasFilter("is_active = TRUE")
            .HasDatabaseName("uq_service_schedule_active");

        builder.HasIndex(e => e.ServiceDefId)
            .HasDatabaseName("ix_service_schedule_service_def");

        builder.HasIndex(e => e.TimeSlotId)
            .HasDatabaseName("ix_service_schedule_time_slot");
    }
}
