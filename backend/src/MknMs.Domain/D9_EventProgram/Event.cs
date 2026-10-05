namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// A bounded, dated occurrence with a name and location.
/// </summary>
/// <remarks>
/// Type is free text. May span multiple days — StartDate and EndDate
/// are both fields. Deactivated, never deleted.
///
/// Specification: §4 (Event), §9.1.11.
/// </remarks>
public class Event
{
    public int EventId { get; set; }

    public string Name { get; set; } = null!;

    public DateOnly StartDate { get; set; }

    public DateOnly EndDate { get; set; }

    public string Location { get; set; } = null!;

    public string Type { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
