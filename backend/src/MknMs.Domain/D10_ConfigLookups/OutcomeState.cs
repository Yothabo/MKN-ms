namespace MknMs.Domain.D10_ConfigLookups;

/// <summary>
/// An administrator-defined fill state of an occurrence. Cancellation
/// is represented as one such state, never as deletion of the
/// occurrence row.
/// </summary>
/// <remarks>
/// Specification: §4 (lookups), §9.1.12, §12.1.3, §15.3.4-15.3.7.
/// </remarks>
public class OutcomeState
{
    public int OutcomeStateId { get; set; }

    public string Name { get; set; } = null!;
}
