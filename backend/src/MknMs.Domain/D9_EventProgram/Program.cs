namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// An ordered schedule belonging to an event. Exactly one Program per
/// Event.
/// </summary>
/// <remarks>
/// Specification: §4 (Program), §9.1.11.
/// </remarks>
public class Program
{
    public int ProgramId { get; set; }

    public int EventId { get; set; }

    public string Title { get; set; } = null!;

    // Navigation
    public Event Event { get; set; } = null!;
}
