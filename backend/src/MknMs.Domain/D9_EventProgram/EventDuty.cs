namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// An event's requirement for a Duty, with a required slot count and
/// an optional Service Definition scope.
/// </summary>
/// <remarks>
/// DutyId is non-null as of Step 7. ServiceDefId is optional; when
/// set, the duty uses that service's per-service rules on this event.
///
/// Specification: §4 (Event Duty), §9.1.11, §10.1.5, §15.8.10.
/// </remarks>
public class EventDuty
{
    public int EventDutyId { get; set; }

    public int EventId { get; set; }

    public int DutyId { get; set; }

    public int? ServiceDefId { get; set; }

    public int RequiredSlotCount { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public Event Event { get; set; } = null!;

    public Duty Duty { get; set; } = null!;

    public ServiceDefinition? ServiceDefinition { get; set; }
}
