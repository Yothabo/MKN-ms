
namespace MknMs.Domain.D13_AttendanceRule;

/// <summary>
/// A single readmission event for a member. Historical fact.
/// </summary>
/// <remarks>
/// One row per readmission. No lifecycle flags. Never removed through
/// normal operations.
///
/// Specification: §4 (Readmission), §13.6, §15.8.23.
/// </remarks>
public class Readmission
{
    public int ReadmissionId { get; set; }

    public int MemberId { get; set; }

    public DateOnly ReadmissionDate { get; set; }

    public int PerformedByAdminId { get; set; }

    public string? Reason { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;

    public Admin PerformedByAdmin { get; set; } = null!;
}
