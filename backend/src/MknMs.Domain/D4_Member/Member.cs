
namespace MknMs.Domain.D4_Member;

/// <summary>
/// A person in the organization's register. Holds exactly one BranchId
/// and exactly one RoleId.
/// </summary>
/// <remarks>
/// MemberStatusId is nullable at this phase; it becomes non-null in
/// Step 7 of the migration ordering. Member.IsActive is retained
/// until Step 8 removes it. ReceiptNumber, CardNumber and JoinReason
/// are optional; CardNumber is permanent once issued.
///
/// Specification: §4 (Member), §9.1.7, §15.8.1, §15.8.2, §15.8.3,
/// §15.8.27.
/// </remarks>
public class Member
{
    public int MemberId { get; set; }

    public string? ReceiptNumber { get; set; }

    public string? CardNumber { get; set; }

    public DateOnly JoinDate { get; set; }

    public string? JoinReason { get; set; }

    public DateOnly DateOfBirth { get; set; }

    public string MembershipStage { get; set; } = null!;

    public string Name { get; set; } = null!;

    public string Surname { get; set; } = null!;

    public string Gender { get; set; } = null!;

    public string Phone { get; set; } = null!;

    public string? Email { get; set; }

    public int BranchId { get; set; }

    public int RoleId { get; set; }

    public int? MemberStatusId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public Branch Branch { get; set; } = null!;

    public Role Role { get; set; } = null!;

    public MemberStatus? MemberStatus { get; set; }
}
