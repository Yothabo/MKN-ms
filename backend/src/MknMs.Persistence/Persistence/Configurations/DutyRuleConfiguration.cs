using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for DutyRule.
/// </summary>
/// <remarks>
/// The only mechanism connecting a Role — or any other criteria type —
/// to a Duty. Multiple rows may share the same (DutyId, TierOrder); the
/// application evaluates them as ANDed criteria.
///
/// CriteriaType is constrained to the fixed vocabulary by the
/// application, not by a CHECK constraint, so that reserved criteria
/// types can be added without a schema migration.
///
/// Specification: §4 (Duty Rule), §9.1.10, §10.1.5.
/// </remarks>
public class DutyRuleConfiguration : IEntityTypeConfiguration<DutyRule>
{
    public void Configure(EntityTypeBuilder<DutyRule> builder)
    {
        builder.HasKey(e => e.RuleId);

        builder.Property(e => e.DutyId)
            .IsRequired();

        builder.Property(e => e.TierOrder)
            .IsRequired();

        builder.Property(e => e.CriteriaType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(e => e.CriteriaValue)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict);

        // Composite index for tier evaluation (5.2's access pattern).
        builder.HasIndex(e => new { e.DutyId, e.TierOrder })
            .HasDatabaseName("ix_duty_rule_duty_tier");
    }
}
