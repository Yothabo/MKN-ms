
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for EntityDeletionPolicy.
/// </summary>
/// <remarks>
/// EntityType is unique: one row per configuration entity type.
///
/// Specification: §4 (Entity Deletion Policy), §9.7, §15.8.15.
/// </remarks>
public class EntityDeletionPolicyConfiguration : IEntityTypeConfiguration<EntityDeletionPolicy>
{
    public void Configure(EntityTypeBuilder<EntityDeletionPolicy> builder)
    {
        builder.HasKey(e => e.PolicyId);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.RequiresApprovalForDelete)
            .IsRequired();

        builder.Property(e => e.RequiresApprovalForDeactivate)
            .IsRequired();

        builder.Property(e => e.RequiredDeletePermissionTierId);

        builder.Property(e => e.RequiredDeactivatePermissionTierId);

        builder.Property(e => e.RequiresReasonForDelete)
            .IsRequired();

        builder.Property(e => e.RequiresReasonForDeactivate)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();

        builder.HasOne(e => e.RequiredDeletePermissionTier)
            .WithMany()
            .HasForeignKey(e => e.RequiredDeletePermissionTierId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.RequiredDeactivatePermissionTier)
            .WithMany()
            .HasForeignKey(e => e.RequiredDeactivatePermissionTierId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => e.EntityType)
            .IsUnique()
            .HasDatabaseName("uq_entity_deletion_policy_entity_type");
    }
}
