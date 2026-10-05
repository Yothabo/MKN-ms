using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for the PermissionTier lookup.
/// </summary>
/// <remarks>
/// Specification: §4 (Permission Tier), §9.1.8.
/// </remarks>
public class PermissionTierConfiguration : IEntityTypeConfiguration<PermissionTier>
{
    public void Configure(EntityTypeBuilder<PermissionTier> builder)
    {
        builder.HasKey(e => e.PermissionTierId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);
    }
}
