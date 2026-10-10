
namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// One branch attending one event. Composite primary key on
/// (EventId, BranchId).
/// </summary>
/// <remarks>
/// The set of branches attending an event is independent of the
/// event's HostBranchId: the host need not appear in the attendee
/// set. No lifecycle flags; the set is derived from the event's own
/// state.
///
/// Specification: §4 (Event Branch), §9.1.11, §15.8.9.
/// </remarks>
public class EventBranch
{
    public int EventId { get; set; }

    public int BranchId { get; set; }

    // Navigation
    public Event Event { get; set; } = null!;

    public Branch Branch { get; set; } = null!;
}
