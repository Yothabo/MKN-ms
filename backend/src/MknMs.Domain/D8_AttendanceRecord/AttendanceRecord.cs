namespace MknMs.Domain.D8_AttendanceRecord;

/// <summary>
/// A recorded fact of a member's presence at an occurrence.
/// </summary>
/// <remarks>
/// Unique on (MemberId, OccurrenceId). Attendance is a raw fact — no
/// interpretation layer, no recency computation, no conditioning on
/// assignment. A subsequent tap for an existing pair is a no-op.
///
/// Specification: §4 (Attendance Record), §13, §15.2.2.
/// </remarks>
public class AttendanceRecord
{
    public int RecordId { get; set; }

    public int MemberId { get; set; }

    public int OccurrenceId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;

    public ServiceOccurrence ServiceOccurrence { get; set; } = null!;
}
