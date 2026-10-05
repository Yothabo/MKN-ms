namespace MknMs.Domain.D3_BranchTimeSlotService;

/// <summary>
/// A materialized instance of a scheduled service on a specific date.
/// Schedule-sourced occurrences carry ScheduleId; event-sourced
/// occurrences carry EventId.
/// </summary>
/// <remarks>
/// Schedule-sourced uniqueness on (ScheduleId, Date), restricted to
/// rows where ScheduleId IS NOT NULL. Provenance is enforced by CHECK
/// constraints in the EF Core configuration.
///
/// Specification: §4 (Service Occurrence), §7, §8.1.2, §15.2.4.
/// </remarks>
public class ServiceOccurrence
{
    public int OccurrenceId { get; set; }

    public int? ScheduleId { get; set; }

    public int? EventId { get; set; }

    public DateOnly Date { get; set; }

    public int? ServiceTypeId { get; set; }

    public TimeOnly? StartTime { get; set; }

    public int? FillStatusId { get; set; }

    public string GeneratedBy { get; set; } = null!;

    public int? CreatedBy { get; set; }

    public int? ChangedBy { get; set; }

    // Navigation
    public ServiceSchedule? Schedule { get; set; }

    public Event? Event { get; set; }

    public ServiceType? ServiceType { get; set; }

    public OutcomeState? FillStatus { get; set; }

    public Admin? CreatedByAdmin { get; set; }

    public Admin? ChangedByAdmin { get; set; }
}
