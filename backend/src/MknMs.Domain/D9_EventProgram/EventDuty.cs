
namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// An ad-hoc role on a program item, with a direct member assignment.
/// Not a reference to the Duty table.
/// </summary>
/// <remarks>
/// Label is free text. AssignedMemberId is a direct pick — no
/// Eligibility or Priority run. AssignmentStatusId is optional.
///
/// Specification: §4 (Event Duty), §9.1.11, §9.7.
/// </remarks>
public class EventDuty
{
    public int EventDutyId { get; set; }

    public int ProgramItemId { get; set; }

    public string Label { get; set; } = null!;

    public int AssignedMemberId { get; set; }

    public int? AssignmentStatusId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public ProgramItem ProgramItem { get; set; } = null!;

    public Member AssignedMember { get; set; } = null!;

    public AssignmentStatus? AssignmentStatus { get; set; }
}
