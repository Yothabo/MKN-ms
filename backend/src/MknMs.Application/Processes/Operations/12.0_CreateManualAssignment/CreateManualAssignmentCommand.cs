namespace MknMs.Application.Processes.Operations.CreateManualAssignment;

/// <summary>
/// Inputs to one Create Manual Assignment invocation.
/// </summary>
/// <remarks>
/// An administrator has selected a (MemberId, DutyId, OccurrenceId)
/// triple, and optionally chosen the target AssignmentStatus. When
/// TargetStatusId is null, the created row enters 7.0's lifecycle
/// exactly like an automatic assignment, and 9.0 is invoked to notify
/// the member. When TargetStatusId is set, the row is created with that
/// status directly and 9.0 is not invoked.
///
/// Specification: §17.1.
/// </remarks>
public sealed record CreateManualAssignmentCommand
{
    public required int MemberId { get; init; }

    public required int DutyId { get; init; }

    public required int OccurrenceId { get; init; }

    /// <summary>
    /// The AssignmentStatus to write at creation, or null to leave the
    /// row awaiting 7.0's lifecycle.
    /// </summary>
    public int? TargetStatusId { get; init; }

    /// <summary>
    /// The administrator performing the action. Written to AssignedBy.
    /// </summary>
    public required int ActingAdminId { get; init; }
}
