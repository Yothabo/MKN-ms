namespace MknMs.Application.Processes.Operations.RecordAttendance;

/// <summary>
/// Inputs to one Record Attendance invocation.
/// </summary>
/// <remarks>
/// Attendance is registered against a specific (MemberId, OccurrenceId)
/// pair. The channel that delivered the signal — an NFC tap at the
/// occurrence, or an administrator's manual entry — is not represented
/// here. §13.1.4: the schema has no field distinguishing manual from
/// tap entry, and the process does not either.
///
/// Specification: §13.
/// </remarks>
public sealed record RecordAttendanceCommand
{
    public required int MemberId { get; init; }

    public required int OccurrenceId { get; init; }
}
