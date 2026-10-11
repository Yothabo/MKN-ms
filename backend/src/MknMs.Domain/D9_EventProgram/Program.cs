
namespace MknMs.Domain.D9_EventProgram;

/// <summary>
/// An ordered schedule belonging to an event. Exactly one Program per
/// Event.
/// </summary>
/// <remarks>
/// Program carries only IsDeleted; it is deactivated by deactivating
/// its Event.
///
/// Specification: §4 (Program), §9.1.11, §9.7.
/// </remarks>
public class Program
{
    public int ProgramId { get; set; }

    public int EventId { get; set; }

    public string Title { get; set; } = null!;

    public bool IsDeleted { get; set; }

    // Navigation
    public Event Event { get; set; } = null!;
}
