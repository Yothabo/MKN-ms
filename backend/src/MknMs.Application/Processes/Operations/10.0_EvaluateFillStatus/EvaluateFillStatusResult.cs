namespace MknMs.Application.Processes.Operations.EvaluateFillStatus;

/// <summary>
/// Summary of one Evaluate Fill Status run.
/// </summary>
/// <remarks>
/// The three counters reflect the mechanical classification written to
/// ServiceOccurrence.FillStatusId. Occurrences skipped because they are
/// already in the cancelled state are counted separately — they were
/// not evaluated and are not part of any of the three.
///
/// Status is Success unless the run threw an exception or refused to
/// run because a required setting was missing.
///
/// Specification: §12.
/// </remarks>
public sealed record EvaluateFillStatusResult
{
    public required string Status { get; init; }

    public required int OccurrencesEvaluated { get; init; }

    public required int UnfilledCount { get; init; }

    public required int PartiallyFilledCount { get; init; }

    public required int FilledCount { get; init; }

    public required int SkippedCancelledCount { get; init; }

    public string? ErrorDetail { get; init; }
}
