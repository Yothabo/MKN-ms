
namespace MknMs.Domain.D14_ConfigurationAudit;

/// <summary>
/// Per-entity-type configuration of the deletion model.
/// </summary>
/// <remarks>
/// One row per configuration entity type. EntityType is unique.
///
/// Specification: §4 (Entity Deletion Policy), §9.7, §15.8.15.
/// </remarks>
public class EntityDeletionPolicy
{
    public int PolicyId { get; set; }

    public string EntityType { get; set; } = null!;

    public bool RequiresApprovalForDelete { get; set; }

    public bool RequiresApprovalForDeactivate { get; set; }

    public int? RequiredDeletePermissionTierId { get; set; }

    public int? RequiredDeactivatePermissionTierId { get; set; }

    public bool RequiresReasonForDelete { get; set; }

    public bool RequiresReasonForDeactivate { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public PermissionTier? RequiredDeletePermissionTier { get; set; }

    public PermissionTier? RequiredDeactivatePermissionTier { get; set; }
}
