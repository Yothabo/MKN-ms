
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Readmission.
/// </summary>
/// <remarks>
/// Specification: §4 (Readmission), §13.6, §15.8.23.
/// </remarks>
public class ReadmissionConfiguration : IEntityTypeConfiguration<Readmission>
{
    public void Configure(EntityTypeBuilder<Readmission> builder)
    {
        builder.HasKey(e => e.ReadmissionId);

        builder.Property(e => e.MemberId)
            .IsRequired();

        builder.Property(e => e.ReadmissionDate)
            .IsRequired();

        builder.Property(e => e.PerformedByAdminId)
            .IsRequired();

        builder.Property(e => e.Reason)
            .HasMaxLength(500);

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.PerformedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.PerformedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MemberId)
            .HasDatabaseName("ix_readmission_member");
    }
}
