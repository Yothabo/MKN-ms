
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for ServiceDefinitionDuty.
/// </summary>
/// <remarks>
/// Composite primary key on (ServiceDefId, DutyId). RequiredSlotCount
/// is administrator-set with no default. A row may be deactivated to
/// stop the duty being required on future occurrences, without
/// affecting existing ones.
///
/// Specification: §4 (Service Definition Duty), §9.1.4, §9.7.
/// </remarks>
public class ServiceDefinitionDutyConfiguration : IEntityTypeConfiguration<ServiceDefinitionDuty>
{
    public void Configure(EntityTypeBuilder<ServiceDefinitionDuty> builder)
    {
        builder.HasKey(e => new { e.ServiceDefId, e.DutyId });

        builder.Property(e => e.RequiredSlotCount)
            .IsRequired();

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();

        builder.HasOne(e => e.ServiceDefinition)
            .WithMany()
            .HasForeignKey(e => e.ServiceDefId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.Duty)
            .WithMany()
            .HasForeignKey(e => e.DutyId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(e => e.DutyId)
            .HasDatabaseName("ix_service_definition_duty_duty");
    }
}
