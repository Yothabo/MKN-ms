namespace MknMs.Domain.D3_BranchTimeSlotService;

/// <summary>
/// A recurring day-and-time-of-day at a branch. Multiple slots per
/// (BranchId, DayOfWeek) are permitted.
/// </summary>
/// <remarks>
/// StartTime does not live here. It lives on ServiceSchedule because it
/// varies per branch, per slot, and per service simultaneously.
///
/// TimeOfDayId references an administrator-defined lookup in D10.
///
/// Specification: §4 (Branch Time Slot), §9.1.3, §9.1.5.
/// </remarks>
public class BranchTimeSlot
{
    public int TimeSlotId { get; set; }

    public int BranchId { get; set; }

    public string DayOfWeek { get; set; } = null!;

    public int TimeOfDayId { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public Branch Branch { get; set; } = null!;

    public TimeOfDay TimeOfDay { get; set; } = null!;
}
