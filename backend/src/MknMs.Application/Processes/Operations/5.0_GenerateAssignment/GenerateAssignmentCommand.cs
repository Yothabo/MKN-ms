namespace MknMs.Application.Processes.Operations.GenerateAssignment;

/// <summary>
/// Inputs to one Generate Assignment run.
/// </summary>
/// <remarks>
/// Two shapes of invocation are supported by the same service:
///
///   - Specific occurrence: fill one occurrence identified by
///     OccurrenceId. Used by the manual admin trigger.
///
///   - Date range: fill every occurrence in [FromDate, ToDate]. Used
///     by the scheduled run and by the manual range trigger.
///
/// Exactly one of OccurrenceId or (FromDate, ToDate) must be supplied.
/// The service validates this and returns a Failure if neither or both
/// are set.
/// </remarks>
public sealed record GenerateAssignmentCommand
{
    public int? OccurrenceId { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }

    public bool IsSingleOccurrence => OccurrenceId is not null;

    public bool IsDateRange => FromDate is not null && ToDate is not null;

    public bool IsValid =>
        (IsSingleOccurrence && !IsDateRange)
        || (!IsSingleOccurrence && IsDateRange);
}
