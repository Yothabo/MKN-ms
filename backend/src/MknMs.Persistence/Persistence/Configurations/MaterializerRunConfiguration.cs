using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for MaterializerRun.
/// </summary>
/// <remarks>
/// Two attribution consistency CHECK constraints (§8.1.5) plus two
/// value restrictions on the enum-like columns.
///
/// Specification: §4 (Materializer Run), §8.1.5.
/// </remarks>
public class MaterializerRunConfiguration : IEntityTypeConfiguration<MaterializerRun>
{
    public void Configure(EntityTypeBuilder<MaterializerRun> builder)
    {
        builder.HasKey(e => e.RunId);

        builder.Property(e => e.TriggerType).IsRequired().HasMaxLength(20);
        builder.Property(e => e.TriggeredBy);
        builder.Property(e => e.StartedAt).IsRequired();
        builder.Property(e => e.CompletedAt);
        builder.Property(e => e.SchedulesEvaluated).IsRequired();
        builder.Property(e => e.OccurrencesCreated).IsRequired();
        builder.Property(e => e.Status).IsRequired().HasMaxLength(20);
        builder.Property(e => e.ErrorDetail);

        builder.HasOne(e => e.TriggeredByAdmin)
            .WithMany()
            .HasForeignKey(e => e.TriggeredBy)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.ToTable(t =>
        {
            t.HasCheckConstraint(
                "chk_materializer_run_trigger_type",
                "trigger_type IN ('Scheduled', 'Manual')");

            t.HasCheckConstraint(
                "chk_materializer_run_status_value",
                "status IN ('Success', 'Failure')");

            t.HasCheckConstraint(
                "chk_materializer_run_trigger",
                "(trigger_type = 'Manual' AND triggered_by IS NOT NULL) OR " +
                "(trigger_type = 'Scheduled' AND triggered_by IS NULL)");

            t.HasCheckConstraint(
                "chk_materializer_run_status",
                "(status = 'Success' AND completed_at IS NOT NULL) OR " +
                "(status = 'Failure' AND error_detail IS NOT NULL)");
        });

        builder.HasIndex(e => e.StartedAt)
            .HasDatabaseName("ix_materializer_run_started_at");
    }
}
