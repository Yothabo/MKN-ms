using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for AttendanceCorrection.
/// </summary>
/// <remarks>
/// Unique on AttendanceRecordId. FK to AttendanceRecord and to Admin.
/// No lifecycle flags: a correction is a recorded fact.
///
/// Specification: §4 (Attendance Correction), §13, §15.8.46.
/// </remarks>
public class AttendanceCorrectionConfiguration : IEntityTypeConfiguration<AttendanceCorrection>
{
    public void Configure(EntityTypeBuilder<AttendanceCorrection> builder)
    {
        builder.HasKey(e => e.CorrectionId);

        builder.Property(e => e.AttendanceRecordId).IsRequired();
        builder.Property(e => e.Reason).HasMaxLength(1000);
        builder.Property(e => e.CorrectedBy).IsRequired();
        builder.Property(e => e.CorrectedAt).IsRequired();

        builder.HasOne(e => e.AttendanceRecord)
            .WithMany()
            .HasForeignKey(e => e.AttendanceRecordId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.CorrectedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.CorrectedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.AttendanceRecordId)
            .IsUnique()
            .HasDatabaseName("uq_attendance_correction_record");
    }
}
