namespace MknMs.Domain.D5_IdentifierHistory;

/// <summary>
/// A single identifier issuance entry for a member. History is
/// preserved across the member's lifetime.
/// </summary>
/// <remarks>
/// Type is free text, not a lookup. No IsActive flag — retirement is
/// expressed via UnassignedDate.
///
/// Specification: §4 (Identifier History), §9.1.7.
/// </remarks>
public class IdentifierHistory
{
    public int EntryId { get; set; }

    public int MemberId { get; set; }

    public string Type { get; set; } = null!;

    public string Number { get; set; } = null!;

    public DateOnly AssignedDate { get; set; }

    public DateOnly? UnassignedDate { get; set; }

    public string? Reason { get; set; }

    public int AuthorizedBy { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;

    public Admin AuthorizedByAdmin { get; set; } = null!;
}
