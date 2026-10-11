
namespace MknMs.Domain.D3_BranchTimeSlotService;

/// <summary>
/// An active instance of a service at a specific time slot. The join of
/// ServiceDefinition and BranchTimeSlot, carrying the concrete StartTime
/// and an IsActive flag.
/// </summary>
/// <remarks>
/// StartTime lives here — not on ServiceDefinition, not on BranchTimeSlot
/// — because it varies per branch, per slot, and per service
/// simultaneously.
///
/// Active-row uniqueness on (ServiceDefId, TimeSlotId). Deactivation
/// stops future materialization without touching existing occurrences.
///
/// Specification: §4 (Service Schedule), §9.1.5, §15.2.3, §9.7.
/// </remarks>
public class ServiceSchedule
{
    public int ScheduleId { get; set; }

    public int ServiceDefId { get; set; }

    public int TimeSlotId { get; set; }

    public TimeOnly StartTime { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public ServiceDefinition ServiceDefinition { get; set; } = null!;

    public BranchTimeSlot TimeSlot { get; set; } = null!;
}
