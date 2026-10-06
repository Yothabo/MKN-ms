namespace MknMs.Application.Processes.Operations.RecordAttendance;

/// <summary>
/// The Record Attendance process (6.0).
/// </summary>
/// <remarks>
/// Writes a single AttendanceRecord row for a (MemberId, OccurrenceId)
/// pair. Raw fact, no interpretation. Idempotent per pair: a repeat
/// invocation is a no-op, not an error.
///
/// Specification: §13.
/// </remarks>
public interface IRecordAttendanceService
{
    Task<RecordAttendanceResult> RunAsync(
        RecordAttendanceCommand command,
        CancellationToken cancellationToken = default);
}
