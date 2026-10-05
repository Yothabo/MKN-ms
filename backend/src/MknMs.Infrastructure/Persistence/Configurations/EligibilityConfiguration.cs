using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Eligibility.
/// </summary>
/// <remarks>
/// Specification: §4 (Eligibility), §9.1.9, §10.1.4.
/// </remarks>
public class EligibilityConfiguration : IEntityTypeConfiguration<Eligibility>
{
    public void Configure(EntityTypeBuilder<Eligibility> builder)
    {
        builder.HasKey(e => e.EligibilityId);

        builder.Property(e => e.MemberId).IsRequired();
        builder.Property(e => e.DutyId).IsRequired();
        builder.Property(e => e.GrantedDate).IsRequired();
        builder.Property(e => e.GrantedBy).IsRequired();
        builder.Property(e => e.RevokedDate);
        builder.Property(e => e.RevokedReason).HasMaxLength(500);

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.GrantedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.GrantedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MemberId, e.DutyId })
            .HasDatabaseName("ix_eligibility_member_duty");
    }
}
