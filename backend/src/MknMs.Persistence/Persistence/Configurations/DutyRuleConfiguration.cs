
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
/// Specification: §4 (Duty Rule), §9.1.10, §10.1.5, §9.7, §15.8.6.
/// </remarks>
public class DutyRuleConfiguration : IEntityTypeConfiguration<DutyRule>
{
    public void Configure(EntityTypeBuilder<DutyRule> builder)
    {
        builder.HasKey(e => e.RuleId);

        builder.Property(e => e.DutyId)
            .IsRequired();

        builder.Property(e => e.ServiceDefId);

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

        builder.Property(e => e.IsDeleted)
            .IsRequired();

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceDefinition)
            .WithMany()
            .HasForeignKey(e => e.ServiceDefId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => new { e.DutyId, e.TierOrder })
            .HasDatabaseName("ix_duty_rule_duty_tier");
    }
}
