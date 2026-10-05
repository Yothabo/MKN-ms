namespace MknMs.Domain.D7_RosterAssignment;

/// <summary>
/// A member assigned to a duty on a specific occurrence.
/// </summary>
/// <remarks>
/// Unique on (MemberId, DutyId, OccurrenceId). AssignmentStatusId is
/// nullable. AssignmentSource is Automatic or Manual. ApprovedBy is
/// currently unused but retained for a possible future approval
/// workflow.
///
/// Specification: §4 (Roster Assignment), §10, §11, §15.
/// </remarks>
public class RosterAssignment
{
    public int AssignmentId { get; set; }

    public int MemberId { get; set; }

    public int DutyId { get; set; }

    public int OccurrenceId { get; set; }

    public int? AssignmentStatusId { get; set; }

    public int? ApprovedBy { get; set; }

    public string AssignmentSource { get; set; } = null!;

    public int? AssignedBy { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;

    public Duty Duty { get; set; } = null!;

    public ServiceOccurrence ServiceOccurrence { get; set; } = null!;

    public AssignmentStatus? AssignmentStatus { get; set; }

    public Admin? ApprovedByAdmin { get; set; }

    public Admin? AssignedByAdmin { get; set; }
}
