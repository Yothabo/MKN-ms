
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for MemberStatus.
/// </summary>
/// <remarks>
/// IsRosterable is the mechanical property the roster engine
/// consults. IsDefault identifies the single default MemberStatus,
/// mirroring Role.IsDefault (OI-2 resolution).
///
/// Specification: §4 (Member Status), §9.1.7, §9.7, §15.8.1.
/// </remarks>
public class MemberStatusConfiguration : IEntityTypeConfiguration<MemberStatus>
{
    public void Configure(EntityTypeBuilder<MemberStatus> builder)
    {
        builder.HasKey(e => e.MemberStatusId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsRosterable)
            .IsRequired();

        builder.Property(e => e.Description)
            .HasMaxLength(1000);

        builder.Property(e => e.IsDefault)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
