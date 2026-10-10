
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for AttributeType.
/// </summary>
/// <remarks>
/// Specification: §4 (Attribute Type), §15.8.4.
/// </remarks>
public class AttributeTypeConfiguration : IEntityTypeConfiguration<AttributeType>
{
    public void Configure(EntityTypeBuilder<AttributeType> builder)
    {
        builder.HasKey(e => e.AttributeTypeId);

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
