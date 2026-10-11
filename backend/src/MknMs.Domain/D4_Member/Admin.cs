
namespace MknMs.Domain.D4_Member;

/// <summary>
/// Links a Member to a PermissionTier. A member with an Admin row is
/// treated as an administrator by the system.
/// </summary>
/// <remarks>
/// Revocation sets IsActive = false; the row remains, preserving
/// attribution on historical records. Acting identities must resolve
/// to an Admin row whose IsActive is true.
///
/// The PermissionTier capability matrix is deferred. Enforcement is
/// upstream, out of scope for the current specification.
///
/// Specification: §4 (Admin), §9.1.8, §15.8.27.
/// </remarks>
public class Admin
{
    public int AdminId { get; set; }

    public int MemberId { get; set; }

    public int PermissionTierId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public Member Member { get; set; } = null!;

    public PermissionTier PermissionTier { get; set; } = null!;
}
