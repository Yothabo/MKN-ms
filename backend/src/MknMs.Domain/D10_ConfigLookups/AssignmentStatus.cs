
namespace MknMs.Domain.D10_ConfigLookups;

/// <summary>
/// An administrator-defined status of a roster assignment.
/// IsTerminal is a capacity concept, not a lifecycle-impermanence
/// concept: a terminal assignment's row remains, and the member may
/// rejoin a slot through a new assignment.
/// </summary>
/// <remarks>
/// IsTerminal is required (amendment §15.1.2). Used by 5.0's
/// availability rule, 7.0's re-resolution logic, and 10.0's capacity
/// calculation.
///
/// Specification: §4 (lookups), §9.1.12, §9.7, §11.1.2, §15.1.2.
/// </remarks>
public class AssignmentStatus
{
    public int AssignmentStatusId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsTerminal { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
