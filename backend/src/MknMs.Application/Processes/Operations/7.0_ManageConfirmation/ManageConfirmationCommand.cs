namespace MknMs.Application.Processes.Operations.ManageConfirmation;

/// <summary>
/// The operation 7.0 is performing.
/// </summary>
/// <remarks>
/// Three shapes, because the process's three triggers map to three
/// different target statuses, and the target status is administrator
/// configuration stored in SystemSetting rather than supplied by the
/// caller:
///
///   - Confirm — a member has confirmed. The assignment transitions to
///     the status named by SystemSetting.InitialAssignmentStatusID,
///     per §11.1.5.
///   - Decline — a member has declined. The assignment transitions to
///     the status named by SystemSetting.DeclinedStatusID, per §11.1.7.
///   - Sweep — the scheduled timeout check. Every NULL-status
///     assignment whose elapsed age meets the configured timeout
///     transitions to the status named by
///     SystemSetting.TimedOutStatusID, per §11.1.7.
///
/// The service reads the target status from the setting, so the caller
/// names only the operation and the assignment (for a single response).
/// This is the reading in which "administrator-designated status" means
/// what it says: a value in a setting, not a value passed by a caller.
///
/// Specification: §11.
/// </remarks>
public enum ManageConfirmationOperation
{
    Confirm = 0,
    Decline = 1,
    Sweep = 2,
}

/// <summary>
/// Inputs to one Manage Confirmation run.
/// </summary>
/// <remarks>
/// For Confirm and Decline, AssignmentId is required.
/// For Sweep, AssignmentId is ignored; the service finds every
/// timed-out assignment itself.
///
/// Specification: §11.
/// </remarks>
public sealed record ManageConfirmationCommand
{
    public required ManageConfirmationOperation Operation { get; init; }

    public int? AssignmentId { get; init; }

    public bool IsSingleResponse =>
        Operation == ManageConfirmationOperation.Confirm
        || Operation == ManageConfirmationOperation.Decline;

    public bool IsValid =>
        (IsSingleResponse && AssignmentId is not null && AssignmentId > 0)
        || (Operation == ManageConfirmationOperation.Sweep && AssignmentId is null);
}
