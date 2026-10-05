using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for Program.
/// </summary>
/// <remarks>
/// Exactly one Program per Event. Enforced by a unique index on
/// EventId.
///
/// Specification: §4 (Program), §9.1.11.
/// </remarks>
public class ProgramConfiguration : IEntityTypeConfiguration<Program>
{
    public void Configure(EntityTypeBuilder<Program> builder)
    {
        builder.HasKey(e => e.ProgramId);

        builder.Property(e => e.EventId).IsRequired();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);

        builder.HasOne(e => e.Event)
            .WithMany()
            .HasForeignKey(e => e.EventId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.EventId)
            .IsUnique()
            .HasDatabaseName("uq_program_event");
    }
}
