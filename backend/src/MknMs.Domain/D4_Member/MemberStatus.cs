
namespace MknMs.Domain.D4_Member;

/// <summary>
/// An administrator-defined state a member may be in.
/// </summary>
/// <remarks>
/// The system does not interpret any status name. IsRosterable is
/// the mechanical property the roster engine consults: a member
/// whose current MemberStatus has IsRosterable = false is excluded
/// from new roster generation. CanAttendRegister is the corresponding
/// property for the attendance register: a member whose current
/// status has CanAttendRegister = false is refused by the register.
/// The two flags are independent.
///
/// IsDefault mirrors Role.IsDefault. Exactly one MemberStatus is
/// the default. The default cannot be soft-deleted, and it is the
/// fallback when a member's status is soft-deleted. IsDefault is
/// added by the OI-2 resolution (see Commit 1 body).
///
/// Specification: §4 (Member Status), §9.1.7, §9.7, §15.8.1.
/// </remarks>
public class MemberStatus
{
    public int MemberStatusId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsRosterable { get; set; }

    /// <summary>
    /// Whether a member whose current status is this one may be
    /// counted present in the attendance register. Independent of
    /// IsRosterable: a member may be rosterable but not attendable
    /// (or the reverse) if the administrator configures it so. The
    /// register's manual and tap paths both consult this flag.
    /// </summary>
    public bool CanAttendRegister { get; set; } = true;

    public string? Description { get; set; }

    public bool IsDefault { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
