
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for MemberAttributeValue.
/// </summary>
/// <remarks>
/// Unique on (MemberId, AttributeTypeId).
///
/// Specification: §4 (Member Attribute Value), §15.8.5.
/// </remarks>
public class MemberAttributeValueConfiguration : IEntityTypeConfiguration<MemberAttributeValue>
{
    public void Configure(EntityTypeBuilder<MemberAttributeValue> builder)
    {
        builder.HasKey(e => e.MemberAttributeValueId);

        builder.Property(e => e.MemberId)
            .IsRequired();

        builder.Property(e => e.AttributeTypeId)
            .IsRequired();

        builder.Property(e => e.Value)
            .IsRequired()
            .HasMaxLength(500);

        builder.Property(e => e.RecordedDate)
            .IsRequired();

        builder.Property(e => e.RecordedBy)
            .IsRequired();

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AttributeType)
            .WithMany()
            .HasForeignKey(e => e.AttributeTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.RecordedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.RecordedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => new { e.MemberId, e.AttributeTypeId })
            .IsUnique()
            .HasDatabaseName("uq_member_attribute_value_member_type");
    }
}
