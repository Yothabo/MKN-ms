
namespace MknMs.Domain.D13_AttendanceRule;

/// <summary>
/// An administrator-defined rule that reads attendance and applies a
/// configured outcome.
/// </summary>
/// <remarks>
/// TriggerType is one of AbsenceDays, ReadmissionCount, Manual.
/// OutcomeType is one of Notify, SetStatus, NoOp. OutcomeStatusId is
/// set only when OutcomeType = SetStatus. Enabled is the evaluation
/// gate; IsActive and IsDeleted are the two-flag lifecycle.
///
/// Specification: §4 (Attendance Rule), §13.6, §15.8.21.
/// </remarks>
public class AttendanceRule
{
    public int AttendanceRuleId { get; set; }

    public string Name { get; set; } = null!;

    public string TriggerType { get; set; } = null!;

    public string? TriggerValue { get; set; }

    public string OutcomeType { get; set; } = null!;

    public int? OutcomeStatusId { get; set; }

    public bool Enabled { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public MemberStatus? OutcomeStatus { get; set; }
}
