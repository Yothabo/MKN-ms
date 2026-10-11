
namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// A bounded, dated occurrence with a name and a host branch.
/// </summary>
/// <remarks>
/// Type is free text. May span multiple days — StartDate and EndDate
/// are both fields. Deactivated, never deleted.
///
/// HostBranchId is nullable at this phase; it becomes non-null in
/// Step 7. Event.Location is retained until Step 8 removes it.
/// UsesAttendanceRegister overrides the global register state for
/// event-sourced occurrences. Null means no override.
///
/// Specification: §4 (Event), §9.1.11, §9.6, §9.7, §15.8.8,
/// §15.8.20.
/// </remarks>
public class Event
{
    public int EventId { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Location { get; set; } = null!;

    public string Type { get; set; } = null!;

    public int? HostBranchId { get; set; }

    public bool? UsesAttendanceRegister { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public Branch? HostBranch { get; set; }
}
