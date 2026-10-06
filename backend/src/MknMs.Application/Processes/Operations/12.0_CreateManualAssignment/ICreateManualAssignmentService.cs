namespace MknMs.Application.Processes.Operations.CreateManualAssignment;

/// <summary>
/// The Create Manual Assignment process (12.0).
/// </summary>
/// <remarks>
/// Creates a RosterAssignment row at an administrator's direction.
/// Bypasses Duty Rule's ranking criteria but respects Eligibility Flag
/// tiers. Does not enforce slot capacity or per-occurrence
/// availability. Enforces the (MemberId, DutyId, OccurrenceId)
/// uniqueness constraint. Invokes 9.0 only when the created row's
/// AssignmentStatusId is null.
///
/// Specification: §17.
/// </remarks>
public interface ICreateManualAssignmentService
{
    Task<CreateManualAssignmentResult> RunAsync(
        CreateManualAssignmentCommand command,
        CancellationToken cancellationToken = default);
}
