namespace MknMs.Domain.D3_BranchTimeSlotService;

/// <summary>
/// A per-occurrence override of the service's normal duty list.
/// </summary>
/// <remarks>
/// Composite primary key on (OccurrenceId, DutyId). Action is Added or
/// Removed, relative to the Service Definition's normal duty list.
/// RequiredSlotCount, when present, overrides the definition's count for
/// this date only.
///
/// Only occurrences with an actual override need a row here. The
/// Materializer never creates rows in this table — they are written by
/// administrators.
///
/// Specification: §4 (Service Occurrence Duty), §7.
/// </remarks>
public class ServiceOccurrenceDuty
{
    public int OccurrenceId { get; set; }

    public int DutyId { get; set; }

    public string Action { get; set; } = null!;

    public int? RequiredSlotCount { get; set; }

    // Navigation
    public ServiceOccurrence ServiceOccurrence { get; set; } = null!;

    public Duty Duty { get; set; } = null!;
}
