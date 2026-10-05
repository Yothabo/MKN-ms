namespace MknMs.Domain.D6_Eligibility;

/// <summary>
/// A binary, administrator-granted permission record for a member
/// against a duty. Independent of DutyRule's ranking logic.
/// </summary>
/// <remarks>
/// Each grant/revoke cycle is its own row. Absence of a row means the
/// member is not eligible.
///
/// Specification: §4 (Eligibility), §9.1.9, §10.1.4.
/// </remarks>
public class Eligibility
{
    public int EligibilityId { get; set; }

    public int MemberId { get; set; }

    public int DutyId { get; set; }

    public DateOnly GrantedDate { get; set; }

    public int GrantedBy { get; set; }

    public DateOnly? RevokedDate { get; set; }

    public string? RevokedReason { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;

    public Duty Duty { get; set; } = null!;

    public Admin GrantedByAdmin { get; set; } = null!;
}
