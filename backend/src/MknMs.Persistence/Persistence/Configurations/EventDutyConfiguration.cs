using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for EventDuty.
/// </summary>
/// <remarks>
/// An event's requirement for a Duty, with a required slot count and
/// an optional Service Definition scope.
///
/// Specification: §4 (Event Duty), §9.1.11, §10.1.5, §15.8.10.
/// </remarks>
public class EventDutyConfiguration : IEntityTypeConfiguration<EventDuty>
{
    public void Configure(EntityTypeBuilder<EventDuty> builder)
    {
        builder.HasKey(e => e.EventDutyId);

        builder.Property(e => e.EventId).IsRequired();
        builder.Property(e => e.DutyId).IsRequired();
        builder.Property(e => e.ServiceDefId);
        builder.Property(e => e.RequiredSlotCount).IsRequired();
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();

        builder.HasOne(e => e.Event)
            .WithMany()
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceDefinition)
            .WithMany()
            .HasForeignKey(e => e.ServiceDefId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => e.EventId)
            .HasDatabaseName("ix_event_duty_event");

        builder.HasIndex(e => e.DutyId)
            .HasDatabaseName("ix_event_duty_duty");
    }
}
