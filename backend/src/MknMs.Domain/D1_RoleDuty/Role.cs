namespace MknMs.Domain.D1_RoleDuty;

/// <summary>
/// A named category a member may hold. An administrator-defined label.
/// Deactivated, never deleted.
/// </summary>
/// <remarks>
/// Specification: §4 (Role), §9.1.2, §9.1.1.
/// </remarks>
public class Role
{
    public int RoleId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
