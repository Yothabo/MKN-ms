namespace MknMs.Domain.D10_ConfigLookups;

/// <summary>
/// An administrator-defined capability level. Kept intentionally
/// minimal — a fuller capability matrix is deferred until it is
/// actually needed.
/// </summary>
/// <remarks>
/// Specification: §4 (Permission Tier), §9.1.8.
/// </remarks>
public class PermissionTier
{
    public int PermissionTierId { get; set; }

    public string Name { get; set; } = null!;
}
