namespace MknMs.Application.Processes.Operations.RecordAttendance;

/// <summary>
/// Outcome of one Record Attendance invocation.
/// </summary>
/// <remarks>
/// Recorded is false when the (MemberId, OccurrenceId) pair already
/// has an AttendanceRecord. This is a no-op, not an error — the
/// uniqueness rule makes the process idempotent per pair (§13.1.2).
/// The existing record's id is returned so the caller can correlate.
///
/// Specification: §13.
/// </remarks>
public sealed record RecordAttendanceResult
{
    public required string Status { get; init; }

    /// <summary>
    /// True when this invocation created a new row. False when an
    /// existing row was found and returned unchanged.
    /// </summary>
    public required bool Recorded { get; init; }

    /// <summary>
    /// The AttendanceRecord id — new when Recorded is true, existing
    /// when Recorded is false.
    /// </summary>
    public required int RecordId { get; init; }

    public string? ErrorDetail { get; init; }
}
