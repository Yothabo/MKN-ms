
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for AttendanceRuleScope.
/// </summary>
/// <remarks>
/// Composite primary key on (AttendanceRuleId, CriteriaType,
/// CriteriaValue). Scope rows for one rule are ANDed.
///
/// Specification: §4 (Attendance Rule Scope), §13.6, §15.8.22.
/// </remarks>
public class AttendanceRuleScopeConfiguration : IEntityTypeConfiguration<AttendanceRuleScope>
{
    public void Configure(EntityTypeBuilder<AttendanceRuleScope> builder)
    {
        builder.HasKey(e => new { e.AttendanceRuleId, e.CriteriaType, e.CriteriaValue });

        builder.Property(e => e.CriteriaType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.CriteriaValue)
            .IsRequired()
            .HasMaxLength(500);

        builder.HasOne(e => e.AttendanceRule)
            .WithMany()
            .HasForeignKey(e => e.AttendanceRuleId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
