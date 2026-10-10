
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for ConfigurationAuditLog.
/// </summary>
/// <remarks>
/// Specification: §4 (Configuration Audit Log), §9.7, §14.1.3,
/// §15.8.14.
/// </remarks>
public class ConfigurationAuditLogConfiguration : IEntityTypeConfiguration<ConfigurationAuditLog>
{
    public void Configure(EntityTypeBuilder<ConfigurationAuditLog> builder)
    {
        builder.HasKey(e => e.AuditId);

        builder.Property(e => e.EntityType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.EntityId)
            .IsRequired();

        builder.Property(e => e.EntityName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.Action)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(e => e.Reason)
            .HasMaxLength(1000);

        builder.Property(e => e.InitiatedByAdminId)
            .IsRequired();

        builder.Property(e => e.ApprovedByAdminId)
            .IsRequired();

        builder.Property(e => e.InitiatedAt)
            .IsRequired();

        builder.Property(e => e.ApprovedAt)
            .IsRequired();

        builder.Property(e => e.ConsequencesPreviewed)
            .IsRequired();

        builder.Property(e => e.ConsequencesOccurred)
            .IsRequired();

        builder.Property(e => e.NotifiedAt);

        builder.HasOne(e => e.InitiatedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.InitiatedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.ApprovedByAdmin)
            .WithMany()
            .HasForeignKey(e => e.ApprovedByAdminId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.NotifiedAt)
            .HasDatabaseName("ix_configuration_audit_log_notified_at");
    }
}
