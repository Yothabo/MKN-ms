using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Admin.
/// </summary>
/// <remarks>
/// Specification: §4 (Admin), §9.1.8.
/// </remarks>
public class AdminConfiguration : IEntityTypeConfiguration<Admin>
{
    public void Configure(EntityTypeBuilder<Admin> builder)
    {
        builder.HasKey(e => e.AdminId);

        builder.Property(e => e.MemberId).IsRequired();
        builder.Property(e => e.PermissionTierId).IsRequired();

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PermissionTier)
            .WithMany()
            .HasForeignKey(e => e.PermissionTierId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MemberId).HasDatabaseName("ix_admin_member");
        builder.HasIndex(e => e.PermissionTierId).HasDatabaseName("ix_admin_tier");
    }
}
