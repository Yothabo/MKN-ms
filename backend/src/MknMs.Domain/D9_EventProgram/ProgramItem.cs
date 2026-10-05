namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// A single entry in a program. Status is not stored — it is computed
/// at read time from ScheduledStart, ScheduledEnd, and the current time.
/// </summary>
/// <remarks>
/// ServiceDefId is optional. When set, the system creates an
/// event-sourced ServiceOccurrence directly (process 8.0). When null,
/// no occurrence is created and the item has no roster machinery.
///
/// Specification: §4 (Program Item), §9.1.11.
/// </remarks>
public class ProgramItem
{
    public int ItemId { get; set; }

    public int ProgramId { get; set; }

    public int SequenceOrder { get; set; }

    public string Title { get; set; } = null!;

    public DateTimeOffset ScheduledStart { get; set; }

    public DateTimeOffset ScheduledEnd { get; set; }

    public int? ServiceDefId { get; set; }

    // Navigation
    public Program Program { get; set; } = null!;

    public ServiceDefinition? ServiceDefinition { get; set; }
}
