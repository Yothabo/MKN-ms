
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for EventDuty.
/// </summary>
/// <remarks>
/// DutyId is nullable at this phase; it becomes non-null in Step 7.
/// Label is retained until Step 8 removes it.
///
/// Specification: §4 (Event Duty), §9.1.11, §9.7, §10.1.5, §15.8.10.
/// </remarks>
public class EventDutyConfiguration : IEntityTypeConfiguration<EventDuty>
{
    public void Configure(EntityTypeBuilder<EventDuty> builder)
    {
        builder.HasKey(e => e.EventDutyId);

        builder.Property(e => e.ProgramItemId).IsRequired();
        builder.Property(e => e.Label).IsRequired().HasMaxLength(200);
        builder.Property(e => e.DutyId);
        builder.Property(e => e.ServiceDefId);
        builder.Property(e => e.AssignedMemberId).IsRequired();
        builder.Property(e => e.AssignmentStatusId);
        builder.Property(e => e.IsActive).IsRequired();
        builder.Property(e => e.IsDeleted).IsRequired();

        builder.HasOne(e => e.ProgramItem)
            .WithMany()
            .HasForeignKey(e => e.ProgramItemId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.ServiceDefinition)
            .WithMany()
            .HasForeignKey(e => e.ServiceDefId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.AssignedMember)
            .WithMany()
            .HasForeignKey(e => e.AssignedMemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AssignmentStatus)
            .WithMany()
            .HasForeignKey(e => e.AssignmentStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => e.ProgramItemId)
            .HasDatabaseName("ix_event_duty_program_item");

        builder.HasIndex(e => e.AssignedMemberId)
            .HasDatabaseName("ix_event_duty_assigned_member");

        builder.HasIndex(e => e.DutyId)
            .HasDatabaseName("ix_event_duty_duty");
    }
}
