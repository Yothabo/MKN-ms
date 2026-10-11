
namespace MknMs.Domain.D1_RoleDuty;

/// <summary>
/// A named category a member may hold. An administrator-defined label.
/// Deactivated, never deleted.
/// </summary>
/// <remarks>
/// IsDefault identifies the single default Role. The default Role
/// cannot be soft-deleted.
///
/// Specification: §4 (Role), §9.1.2, §9.7, §15.8.13.
/// </remarks>
public class Role
{
    public int RoleId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
