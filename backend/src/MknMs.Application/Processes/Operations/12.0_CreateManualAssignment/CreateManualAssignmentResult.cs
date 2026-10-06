namespace MknMs.Application.Processes.Operations.CreateManualAssignment;

/// <summary>
/// Outcome of one Create Manual Assignment invocation.
/// </summary>
/// <remarks>
/// AssignmentId is the primary key of the created row.
/// NotificationDispatched is true when the created row had a null
/// AssignmentStatusId and 9.0 was invoked. It is false both when the
/// admin supplied a status and when the creation failed.
///
/// Specification: §17.
/// </remarks>
public sealed record CreateManualAssignmentResult
{
    public required string Status { get; init; }

    public required int AssignmentId { get; init; }

    public required bool NotificationDispatched { get; init; }

    public string? ErrorDetail { get; init; }
}
