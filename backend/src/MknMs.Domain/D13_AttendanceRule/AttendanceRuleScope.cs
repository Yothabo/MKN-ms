
namespace MknMs.Domain.D13_AttendanceRule;

/// <summary>
/// A single scope criterion for an Attendance Rule. Multiple rows for
/// one rule are ANDed.
/// </summary>
/// <remarks>
/// Composite primary key on (AttendanceRuleId, CriteriaType,
/// CriteriaValue). CriteriaType uses the same vocabulary as
/// DutyRule.
///
/// Specification: §4 (Attendance Rule Scope), §13.6, §15.8.22.
/// </remarks>
public class AttendanceRuleScope
{
    public int AttendanceRuleId { get; set; }

    public string CriteriaType { get; set; } = null!;

    public string CriteriaValue { get; set; } = null!;

    // Navigation
    public AttendanceRule AttendanceRule { get; set; } = null!;
}
