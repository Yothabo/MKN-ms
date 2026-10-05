using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for AttendanceRecord.
/// </summary>
/// <remarks>
/// Unique on (MemberId, OccurrenceId) — amendment §15.2.2. Attendance
/// is a raw fact; no interpretation layer.
///
/// Specification: §4 (Attendance Record), §13, §15.2.2.
/// </remarks>
public class AttendanceRecordConfiguration : IEntityTypeConfiguration<AttendanceRecord>
{
    public void Configure(EntityTypeBuilder<AttendanceRecord> builder)
    {
        builder.HasKey(e => e.RecordId);

        builder.Property(e => e.MemberId).IsRequired();
        builder.Property(e => e.OccurrenceId).IsRequired();
        builder.Property(e => e.Timestamp).IsRequired();

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceOccurrence)
            .WithMany()
            .HasForeignKey(e => e.OccurrenceId)
            .OnDelete(DeleteBehavior.Restrict);

        // Amendment §15.2.2 — uniqueness.
        builder.HasIndex(e => new { e.MemberId, e.OccurrenceId })
            .IsUnique()
            .HasDatabaseName("uq_attendance_record_member_occurrence");

        builder.HasIndex(e => e.OccurrenceId)
            .HasDatabaseName("ix_attendance_record_occurrence");

        builder.HasIndex(e => new { e.MemberId, e.Timestamp })
            .HasDatabaseName("ix_attendance_record_member_ts");
    }
}
