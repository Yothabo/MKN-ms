namespace MknMs.Application.Processes.Operations.ManageConfirmation;

/// <summary>
/// Inputs to one Manage Confirmation run.
/// </summary>
/// <remarks>
/// Two shapes of invocation are supported by the same service:
///
///   - Respond: a member has confirmed or declined. The caller supplies
///     the assignment and the administrator-designated status ID that
///     the member's response maps to. The service applies the
///     transition; if the target status is terminal, it re-resolves the
///     affected slot.
///
///   - Sweep: the scheduled timeout check. The caller supplies the
///     administrator-designated status ID that a timed-out assignment
///     maps to. The service finds every NULL-status assignment whose
///     elapsed age meets the configured timeout and applies the
///     transition to each; for each, if the target status is terminal,
///     it re-resolves the affected slot.
///
/// The service does not decide which status ID means "confirmed" or
/// "declined" or "timed out". That mapping is administrator
/// configuration, and the caller supplies the target ID. This is the
/// only reading that does not require a setting the specification does
/// not name.
///
/// Specification: §11.
/// </remarks>
public sealed record ManageConfirmationCommand
{
    /// <summary>
    /// Respond to a specific assignment. When set, TargetStatusId is
    /// required and the sweep fields must be null.
    /// </summary>
    public int? AssignmentId { get; init; }

    /// <summary>
    /// The status the assignment transitions to. Required for a Respond
    /// command. Null for a Sweep command.
    /// </summary>
    public int? TargetStatusId { get; init; }

    /// <summary>
    /// Sweep timed-out assignments. When true, TargetStatusId is
    /// required and AssignmentId must be null.
    /// </summary>
    public bool IsSweep { get; init; }

    public bool IsRespond => AssignmentId is not null && !IsSweep;

    public bool IsValid =>
        (IsRespond && TargetStatusId is not null)
        || (IsSweep && AssignmentId is null && TargetStatusId is not null);
}
