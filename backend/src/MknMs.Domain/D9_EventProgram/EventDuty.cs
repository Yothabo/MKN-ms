
namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// An event's requirement for a Duty, with a required slot count and
/// an optional Service Definition scope.
/// </summary>
/// <remarks>
/// DutyId is nullable at this phase; it becomes non-null in Step 7.
/// Label is retained until Step 8 removes it. ServiceDefId is
/// optional; when set, the duty uses that service's per-service
/// rules on this event.
///
/// Specification: §4 (Event Duty), §9.1.11, §9.7, §10.1.5, §15.8.10.
/// </remarks>
public class EventDuty
{
    public int EventDutyId { get; set; }

    public int ProgramItemId { get; set; }

    public string Label { get; set; } = null!;

    public int? DutyId { get; set; }

    public int? ServiceDefId { get; set; }

    public int AssignedMemberId { get; set; }

    public int? AssignmentStatusId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public ProgramItem ProgramItem { get; set; } = null!;

    public Duty? Duty { get; set; }

    public ServiceDefinition? ServiceDefinition { get; set; }

    public Member AssignedMember { get; set; } = null!;

    public AssignmentStatus? AssignmentStatus { get; set; }
}
