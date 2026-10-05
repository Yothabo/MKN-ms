using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for ServiceOccurrence.
/// </summary>
/// <remarks>
/// Schedule-sourced occurrences carry ScheduleId; event-sourced
/// occurrences carry EventId.
///
/// Schedule-sourced uniqueness (amendment §15.2.4): (ScheduleId, Date)
/// unique among rows where ScheduleId IS NOT NULL.
///
/// Provenance consistency (amendment §8.1.2): enforced by CHECK
/// constraints.
///
/// Specification: §4 (Service Occurrence), §7, §8.1.2, §15.2.4.
/// </remarks>
public class ServiceOccurrenceConfiguration : IEntityTypeConfiguration<ServiceOccurrence>
{
    public void Configure(EntityTypeBuilder<ServiceOccurrence> builder)
    {
        builder.HasKey(e => e.OccurrenceId);

        builder.Property(e => e.ScheduleId);
        builder.Property(e => e.EventId);
        builder.Property(e => e.Date).IsRequired();
        builder.Property(e => e.ServiceTypeId);
        builder.Property(e => e.StartTime);
        builder.Property(e => e.FillStatusId);
        builder.Property(e => e.GeneratedBy).IsRequired().HasMaxLength(50);
        builder.Property(e => e.CreatedBy);
        builder.Property(e => e.ChangedBy);

        // Foreign keys — each navigation explicitly wired to its FK.
        builder.HasOne(e => e.Schedule)
            .WithMany()
            .HasForeignKey(e => e.ScheduleId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.Event)
            .WithMany()
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.ServiceType)
            .WithMany()
            .HasForeignKey(e => e.ServiceTypeId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.FillStatus)
            .WithMany()
            .HasForeignKey(e => e.FillStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Two navigations to Admin. Explicit FK wiring resolves the ambiguity.
        builder.HasOne(e => e.CreatedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.CreatedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.ChangedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.ChangedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // CHECK constraints.
        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "chk_service_occurrence_generated_by",
                "generated_by IN ('System', 'Administrator')");

            t.HasCheckConstraint(
                "chk_service_occurrence_provenance",
                "(generated_by = 'System' AND created_by IS NULL) OR " +
                "(generated_by = 'Administrator' AND created_by IS NOT NULL)");
        });

        // Partial unique index for schedule-sourced occurrences.
        builder.HasIndex(e => new { e.ScheduleId, e.Date })
            .IsUnique()
            .HasFilter("schedule_id IS NOT NULL")
            .HasDatabaseName("uq_service_occurrence_schedule_date");

        builder.HasIndex(e => e.Date)
            .HasDatabaseName("ix_service_occurrence_date");
    }
}
