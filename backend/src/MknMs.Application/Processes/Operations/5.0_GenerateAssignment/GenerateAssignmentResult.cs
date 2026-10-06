namespace MknMs.Application.Processes.Operations.GenerateAssignment;

/// <summary>
/// Summary of one Generate Assignment run.
/// </summary>
/// <remarks>
/// Counts and outcome of the run. The status is Success if the run
/// completed without an exception, regardless of whether any
/// assignments were created. "No candidates available for a duty" is
/// not a failure — it is a legitimate outcome per §10.1.5, and it is
/// reflected in the counts.
///
/// ErrorDetail is populated only when the run threw an exception that
/// the service caught and recorded.
/// </remarks>
public sealed record GenerateAssignmentResult
{
    public required string Status { get; init; }

    public required int OccurrencesProcessed { get; init; }

    public required int AssignmentsCreated { get; init; }

    public required int DutiesFullyFilled { get; init; }

    public required int DutiesPartiallyFilled { get; init; }

    public required int DutiesUnfilled { get; init; }

    public string? ErrorDetail { get; init; }
}
