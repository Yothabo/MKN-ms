namespace MknMs.Domain.D4_Member;

/// <summary>
/// A person in the organization's register. Holds exactly one BranchId
/// and exactly one RoleId.
/// </summary>
/// <remarks>
/// Required at creation: JoinDate, DateOfBirth, MembershipStage, Name,
/// Surname, Gender, Phone, BranchId, RoleId. Only Email is optional.
///
/// MembershipStage is a free-text administrator-defined label, not a
/// lookup table. Deactivation excludes the member from new roster
/// generation; it does not touch operational records.
///
/// Specification: §4 (Member), §9.1.7.
/// </remarks>
public class Member
{
    public int MemberId { get; set; }

    public DateOnly JoinDate { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string MembershipStage { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Gender { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public int BranchId { get; set; }

    public int RoleId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public Branch Branch { get; set; } = null!;

    public Role Role { get; set; } = null!;
}
