
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Capability.
/// </summary>
/// <remarks>
/// The Capability vocabulary names actions. No relationship to
/// Role, Admin, or PermissionTier is mapped; the assignment of
/// capabilities is deliberately deferred.
///
/// Specification: §4 (Capability), §15.8.17.
/// </remarks>
public class CapabilityConfiguration : IEntityTypeConfiguration<Capability>
{
    public void Configure(EntityTypeBuilder<Capability> builder)
    {
        builder.HasKey(e => e.CapabilityId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
