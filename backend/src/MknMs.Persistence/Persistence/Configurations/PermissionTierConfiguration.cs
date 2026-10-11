
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the PermissionTier lookup.
/// </summary>
/// <remarks>
/// IsDefault identifies the single default tier, mirroring Role.
/// IsDefault. Added by the OI-2 resolution so §9.7's "the default" is
/// executable.
///
/// Specification: §4 (Permission Tier), §9.1.8, §9.7.
/// </remarks>
public class PermissionTierConfiguration : IEntityTypeConfiguration<PermissionTier>
{
    public void Configure(EntityTypeBuilder<PermissionTier> builder)
    {
        builder.HasKey(e => e.PermissionTierId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsDefault)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
