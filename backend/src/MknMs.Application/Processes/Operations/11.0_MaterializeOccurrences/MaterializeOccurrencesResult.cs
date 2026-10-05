namespace MknMs.Application.Processes.Operations.MaterializeOccurrences;

/// <summary>
/// Summary of one Materialize Occurrences run.
/// </summary>
/// <remarks>
/// Mirrors the fields written to the materializer_run row, with a
/// success/failure distinction that determines whether the run
/// completes normally or throws a configuration error.
/// </remarks>
public sealed record MaterializeOccurrencesResult
{
    public required int RunId { get; init; }

    public required string Status { get; init; }

    public required int SchedulesEvaluated { get; init; }

    public required int OccurrencesCreated { get; init; }

    public string? ErrorDetail { get; init; }
}
