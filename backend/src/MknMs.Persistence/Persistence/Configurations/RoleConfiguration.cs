using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Role.
/// </summary>
/// <remarks>
/// A named category a member may hold. No relationship to Duty. Names
/// are required but need not be unique.
///
/// Specification: §4 (Role), §9.1.2.
/// </remarks>
public class RoleConfiguration : IEntityTypeConfiguration<Role>
{
    public void Configure(EntityTypeBuilder<Role> builder)
    {
        builder.HasKey(e => e.RoleId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDefault)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();
    }
}
