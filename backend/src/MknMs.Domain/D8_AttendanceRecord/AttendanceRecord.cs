
namespace MknMs.Domain.D8_AttendanceRecord;

/// <summary>
/// A recorded fact of a member's presence at an occurrence.
/// </summary>
/// <remarks>
/// Unique on (MemberId, OccurrenceId). Attendance is a raw fact — no
/// interpretation layer, no recency computation, no conditioning on
/// assignment. A subsequent tap for an existing pair is a no-op.
///
/// Source names the channel: Manual for an administrator's direct
/// entry, Tap for the NFC tap path. Tap is defined in the vocabulary
/// but is not produced by any implemented path in the current phase.
///
/// Specification: §4 (Attendance Record), §13, §15.2.2, §15.8.19.
/// </remarks>
public class AttendanceRecord
{
    public int RecordId { get; set; }

    public int MemberId { get; set; }

    public int OccurrenceId { get; set; }

    public DateTimeOffset Timestamp { get; set; }

    public string Source { get; set; } = null!;

    // Navigation
    public Member Member { get; set; } = null!;

    public ServiceOccurrence ServiceOccurrence { get; set; } = null!;
}
