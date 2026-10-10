
namespace MknMs.Domain.D10_ConfigLookups;

/// <summary>
/// An administrator-defined name for an action the system can
/// perform.
/// </summary>
/// <remarks>
/// The Capability vocabulary names actions. It carries no reference
/// to Role, Admin, or PermissionTier. The assignment of capabilities
/// to roles, tiers, or admins is deliberately deferred by the
/// specification and is not modelled here.
///
/// Specification: §4 (Capability), §15.8.17.
/// </remarks>
public class Capability
{
    public int CapabilityId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
