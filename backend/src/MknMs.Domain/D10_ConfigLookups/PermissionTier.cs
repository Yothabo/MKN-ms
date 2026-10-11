
namespace MknMs.Domain.D10_ConfigLookups;

/// <summary>
/// An administrator-defined capability level. Kept intentionally
/// minimal — a fuller capability matrix is deferred until it is
/// actually needed.
/// </summary>
/// <remarks>
/// IsDefault identifies the single default PermissionTier, mirroring
/// Role.IsDefault. The default cannot be soft-deleted. Added by the
/// OI-2 resolution so §9.7's "the default" is executable.
///
/// Specification: §4 (Permission Tier), §9.1.8, §9.7.
/// </remarks>
public class PermissionTier
{
    public int PermissionTierId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
