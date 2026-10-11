
namespace MknMs.Domain.D3_BranchTimeSlotService;

/// <summary>
/// A physical location where services run.
/// </summary>
/// <remarks>
/// Location is a single structured value. Its internal representation is
/// an open implementation choice (§9.5).
///
/// UsesAttendanceRegister overrides the global register state for
/// schedule-sourced occurrences at this branch. Null means no
/// override.
///
/// Specification: §4 (Branch), §9.1.3, §9.6, §9.7, §15.8.20.
/// </remarks>
public class Branch
{
    public int BranchId { get; set; }

    public string Name { get; set; } = null!;

    public string Location { get; set; } = null!;

    public bool? UsesAttendanceRegister { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
