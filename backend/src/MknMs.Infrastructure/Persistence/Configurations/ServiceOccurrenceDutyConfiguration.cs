using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for ServiceOccurrenceDuty.
/// </summary>
/// <remarks>
/// Composite primary key on (OccurrenceId, DutyId). Action is Added or
/// Removed. RequiredSlotCount, when present, overrides the definition's
/// count for this date only.
///
/// Specification: §4 (Service Occurrence Duty), §7.
/// </remarks>
public class ServiceOccurrenceDutyConfiguration : IEntityTypeConfiguration<ServiceOccurrenceDuty>
{
    public void Configure(EntityTypeBuilder<ServiceOccurrenceDuty> builder)
    {
        builder.HasKey(e => new { e.OccurrenceId, e.DutyId });

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(e => e.RequiredSlotCount);

        builder.HasOne(e => e.ServiceOccurrence)
            .WithMany()
            .HasForeignKey(e => e.OccurrenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_service_occurrence_duty_action",
            "action IN ('Added', 'Removed')"));

        builder.HasIndex(e => e.DutyId)
            .HasDatabaseName("ix_service_occurrence_duty_duty");
    }
}
