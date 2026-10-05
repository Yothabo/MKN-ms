using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for RosterAssignment.
/// </summary>
/// <remarks>
/// Unique on (MemberId, DutyId, OccurrenceId) — amendment §15.2.1.
/// AssignmentStatusId is nullable — amendment §15.4.1. CreatedAt is
/// required — amendment §15.1.1.
///
/// Specification: §4 (Roster Assignment), §10, §11, §15.
/// </remarks>
public class RosterAssignmentConfiguration : IEntityTypeConfiguration<RosterAssignment>
{
    public void Configure(EntityTypeBuilder<RosterAssignment> builder)
    {
        builder.HasKey(e => e.AssignmentId);

        builder.Property(e => e.MemberId).IsRequired();
        builder.Property(e => e.DutyId).IsRequired();
        builder.Property(e => e.OccurrenceId).IsRequired();
        builder.Property(e => e.AssignmentStatusId);
        builder.Property(e => e.ApprovedBy);
        builder.Property(e => e.AssignmentSource).IsRequired().HasMaxLength(20);
        builder.Property(e => e.AssignedBy);
        builder.Property(e => e.CreatedAt).IsRequired();

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceOccurrence)
            .WithMany()
            .HasForeignKey(e => e.OccurrenceId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AssignmentStatus)
            .WithMany()
            .HasForeignKey(e => e.AssignmentStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        // Two navigations to Admin. Explicit FK wiring resolves ambiguity.
        builder.HasOne(e => e.ApprovedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.ApprovedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.AssignedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.AssignedBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => new { e.MemberId, e.DutyId, e.OccurrenceId })
            .IsUnique()
            .HasDatabaseName("uq_roster_assignment_member_duty_occurrence");

        builder.HasIndex(e => new { e.OccurrenceId, e.DutyId })
            .HasDatabaseName("ix_roster_assignment_occurrence_duty");

        builder.HasIndex(e => e.CreatedAt)
            .HasDatabaseName("ix_roster_assignment_created_at");

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_roster_assignment_source",
            "assignment_source IN ('Automatic', 'Manual')"));
    }
}
