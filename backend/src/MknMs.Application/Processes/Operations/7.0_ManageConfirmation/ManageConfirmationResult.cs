namespace MknMs.Application.Processes.Operations.ManageConfirmation;

/// <summary>
/// Summary of one Manage Confirmation run.
/// </summary>
/// <remarks>
/// The counts describe what the run did:
///
///   - AssignmentsTransitioned: status transitions applied (a Respond
///     command applies at most one; a Sweep command can apply many).
///   - ReplacementsCreated: replacement RosterAssignment rows inserted
///     after a terminal transition. Each is the result of a successful
///     re-resolution.
///   - SlotsLeftVacant: terminal transitions where re-resolution found
///     no candidate. The slot remains unfilled.
///
/// Status is Success unless the run threw an exception the service
/// caught and recorded. A sweep that finds no timed-out assignments is
/// still a Success.
///
/// ErrorDetail is populated only when the run returned a Failure.
///
/// Specification: §11.
/// </remarks>
public sealed record ManageConfirmationResult
{
    public required string Status { get; init; }

    public required int AssignmentsTransitioned { get; init; }

    public required int ReplacementsCreated { get; init; }

    public required int SlotsLeftVacant { get; init; }

    public string? ErrorDetail { get; init; }
}
