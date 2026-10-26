namespace MknMs.Domain.D13_AttendanceRule;

/// <summary>
/// A correction to a recorded AttendanceRecord. The record itself is
/// never modified or deleted; a correction is a second row that
/// names the corrected mark, records who corrected it and when, and
/// optionally carries a reason.
/// </summary>
/// <remarks>
/// Unique on AttendanceRecordId: a record may be corrected once.
/// A second correction is rejected with DuplicateName.
///
/// Specification: §4 (Attendance Correction), §13, §15.8.46.
/// </remarks>
public class AttendanceCorrection
{
    public int CorrectionId { get; set; }

    public int AttendanceRecordId { get; set; }

    public string? Reason { get; set; }

    public int CorrectedBy { get; set; }

    public DateTimeOffset CorrectedAt { get; set; }

    // Navigation
    public AttendanceRecord AttendanceRecord { get; set; } = null!;

    public Admin CorrectedByAdmin { get; set; } = null!;
}
