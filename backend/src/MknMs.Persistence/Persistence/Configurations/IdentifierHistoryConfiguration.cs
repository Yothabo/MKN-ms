using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for IdentifierHistory.
/// </summary>
/// <remarks>
/// Specification: §4 (Identifier History), §9.1.7.
/// </remarks>
public class IdentifierHistoryConfiguration : IEntityTypeConfiguration<IdentifierHistory>
{
    public void Configure(EntityTypeBuilder<IdentifierHistory> builder)
    {
        builder.HasKey(e => e.EntryId);

        builder.Property(e => e.MemberId).IsRequired();
        builder.Property(e => e.Type).IsRequired().HasMaxLength(100);
        builder.Property(e => e.Number).IsRequired().HasMaxLength(100);
        builder.Property(e => e.AssignedDate).IsRequired();
        builder.Property(e => e.UnassignedDate);
        builder.Property(e => e.Reason).HasMaxLength(500);
        builder.Property(e => e.AuthorizedBy).IsRequired();

        builder.HasOne(e => e.Member)
            .WithMany()
            .HasForeignKey(e => e.MemberId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.AuthorizedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.AuthorizedBy)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.MemberId).HasDatabaseName("ix_identifier_history_member");
    }
}
