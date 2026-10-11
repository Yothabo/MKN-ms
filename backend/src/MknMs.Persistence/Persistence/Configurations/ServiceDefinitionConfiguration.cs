
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for ServiceDefinition.
/// </summary>
/// <remarks>
/// ServiceTypeId is required; OwningBranchId is optional. When null,
/// the service is available to any branch. When set, it is exclusive
/// to that branch — a rule enforced when a ServiceSchedule is created
/// or edited (process 1.6), not here.
///
/// Specification: §4 (Service Definition), §9.1.4, §9.1.6, §9.7.
/// </remarks>
public class ServiceDefinitionConfiguration : IEntityTypeConfiguration<ServiceDefinition>
{
    public void Configure(EntityTypeBuilder<ServiceDefinition> builder)
    {
        builder.HasKey(e => e.ServiceDefId);

        builder.Property(e => e.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.ServiceTypeId)
            .IsRequired();

        builder.Property(e => e.OwningBranchId);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();

        builder.HasOne(e => e.ServiceType)
            .WithMany()
            .HasForeignKey(e => e.ServiceTypeId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne(e => e.OwningBranch)
            .WithMany()
            .HasForeignKey(e => e.OwningBranchId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasIndex(e => e.ServiceTypeId)
            .HasDatabaseName("ix_service_definition_service_type");

        builder.HasIndex(e => e.OwningBranchId)
            .HasDatabaseName("ix_service_definition_owning_branch");
    }
}
