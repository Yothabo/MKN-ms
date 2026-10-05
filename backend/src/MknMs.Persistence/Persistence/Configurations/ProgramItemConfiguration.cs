using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for ProgramItem.
/// </summary>
/// <remarks>
/// Status is not stored — it is computed at read time from
/// ScheduledStart, ScheduledEnd, and the current time. ServiceDefId is
/// optional; when set, process 8.0 creates an event-sourced occurrence.
///
/// Specification: §4 (Program Item), §9.1.11.
/// </remarks>
public class ProgramItemConfiguration : IEntityTypeConfiguration<ProgramItem>
{
    public void Configure(EntityTypeBuilder<ProgramItem> builder)
    {
        builder.HasKey(e => e.ItemId);

        builder.Property(e => e.ProgramId).IsRequired();
        builder.Property(e => e.SequenceOrder).IsRequired();
        builder.Property(e => e.Title).IsRequired().HasMaxLength(200);
        builder.Property(e => e.ScheduledStart).IsRequired();
        builder.Property(e => e.ScheduledEnd).IsRequired();
        builder.Property(e => e.ServiceDefId);

        builder.HasOne(e => e.Program)
            .WithMany()
            .HasForeignKey(e => e.ProgramId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ServiceDefinition)
            .WithMany()
            .HasForeignKey(e => e.ServiceDefId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => e.ProgramId)
            .HasDatabaseName("ix_program_item_program");

        builder.HasIndex(e => e.ServiceDefId)
            .HasDatabaseName("ix_program_item_service_def");
    }
}
