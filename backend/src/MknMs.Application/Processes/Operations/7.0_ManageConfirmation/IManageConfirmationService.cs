namespace MknMs.Application.Processes.Operations.ManageConfirmation;

/// <summary>
/// The Manage Confirmation process (7.0).
/// </summary>
/// <remarks>
/// Tracks each RosterAssignment's response lifecycle. Transitions the
/// assignment's AssignmentStatusId from NULL to an administrator-
/// designated status. On a decline or timeout (a transition to a
/// terminal status), re-resolves the affected (OccurrenceId, DutyId)
/// slot and inserts a replacement assignment if a candidate is found.
///
/// Owns the assignment status lifecycle from where 5.0 leaves off.
/// 5.0 creates assignments with AssignmentStatusId = NULL; 7.0 owns
/// every transition after that. Invokes 9.0 Dispatch Notification on
/// each replacement created. Fire-and-forget.
///
/// Specification: §11.
/// </remarks>
public interface IManageConfirmationService
{
    Task<ManageConfirmationResult> RunAsync(
        ManageConfirmationCommand command,
        CancellationToken cancellationToken = default);
}
