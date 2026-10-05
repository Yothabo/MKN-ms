using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Branch.
/// </summary>
/// <remarks>
/// Location is a single structured value stored as TEXT. Its internal
/// representation is an open implementation choice (§9.5).
///
/// Specification: §4 (Branch), §9.1.3.
/// </remarks>
public class BranchConfiguration : IEntityTypeConfiguration<Branch>
{
    public void Configure(EntityTypeBuilder<Branch> builder)
    {
        builder.HasKey(e => e.BranchId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Location)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();
    }
}
