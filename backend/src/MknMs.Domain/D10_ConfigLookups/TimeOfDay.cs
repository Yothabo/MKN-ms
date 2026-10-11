
namespace MknMs.Domain.D10_ConfigLookups;

/// <summary>
/// A coarse administrator-defined time-of-day classification. Not a
/// time — the actual clock time lives on ServiceSchedule.StartTime.
/// </summary>
/// <remarks>
/// Specification: §4 (lookups), §9.1.3, §9.1.12, §9.7.
/// </remarks>
public class TimeOfDay
{
    public int TimeOfDayId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
