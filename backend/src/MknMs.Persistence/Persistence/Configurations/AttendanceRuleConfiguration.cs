
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for AttendanceRule.
/// </summary>
/// <remarks>
/// Specification: §4 (Attendance Rule), §13.6, §15.8.21.
/// </remarks>
public class AttendanceRuleConfiguration : IEntityTypeConfiguration<AttendanceRule>
{
    public void Configure(EntityTypeBuilder<AttendanceRule> builder)
    {
        builder.HasKey(e => e.AttendanceRuleId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.TriggerType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.TriggerValue)
            .HasMaxLength(200);

        builder.Property(e => e.OutcomeType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.OutcomeStatusId);

        builder.Property(e => e.Enabled)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();

        builder.HasOne(e => e.OutcomeStatus)
            .WithMany()
            .HasForeignKey(e => e.OutcomeStatusId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => e.Enabled)
            .HasDatabaseName("ix_attendance_rule_enabled");
    }
}
