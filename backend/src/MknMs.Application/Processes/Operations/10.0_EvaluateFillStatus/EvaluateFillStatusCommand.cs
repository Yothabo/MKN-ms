namespace MknMs.Application.Processes.Operations.EvaluateFillStatus;

/// <summary>
/// Inputs to one Evaluate Fill Status run.
/// </summary>
/// <remarks>
/// The process may be invoked for a single occurrence or swept across
/// a date range. Both shapes are supported by the same service.
///
///   - Single: OccurrenceId is set. Evaluate that occurrence.
///   - Range: FromDate and ToDate are set. Evaluate every occurrence
///     with Date in [FromDate, ToDate].
///
/// The range form is what the scheduled sweep uses. The single form is
/// what an event-driven invocation uses after 5.0 or 7.0 writes an
/// assignment.
///
/// Specification: §12.
/// </remarks>
public sealed record EvaluateFillStatusCommand
{
    public int? OccurrenceId { get; init; }

    public DateOnly? FromDate { get; init; }

    public DateOnly? ToDate { get; init; }

    public bool IsSingle => OccurrenceId is not null;

    public bool IsRange => FromDate is not null && ToDate is not null;

    public bool IsValid =>
        (IsSingle && !IsRange) || (!IsSingle && IsRange);
}
