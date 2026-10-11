
namespace MknMs.Application.Processes.Operations.AttendanceRuleEngine;

/// <summary>
/// Inputs to one Attendance Rule Engine run.
/// </summary>
/// <remarks>
/// Two shapes.
///
///   - Scheduled run: TriggerType is Scheduled; RuleId and
///     ActingAdminId are null. Every enabled rule whose TriggerType
///     is not Manual is evaluated in ascending AttendanceRuleId
///     order.
///
///   - Manual invocation: TriggerType is Manual; RuleId names one
///     rule and ActingAdminId names the administrator performing
///     the action. Only that rule is evaluated.
///
/// Specification: §13.6, §16; contracts.md "13.0 Attendance Rule
/// Engine".
/// </remarks>
public sealed record AttendanceRuleEngineCommand
{
    public required string TriggerType { get; init; }

    public int? RuleId { get; init; }

    public int? ActingAdminId { get; init; }

    public bool IsScheduled =>
        string.Equals(TriggerType, "Scheduled", StringComparison.Ordinal);

    public bool IsManual =>
        string.Equals(TriggerType, "Manual", StringComparison.Ordinal);

    public bool IsValid =>
        (IsScheduled && RuleId is null && ActingAdminId is null)
        || (IsManual && RuleId is not null && RuleId > 0
            && ActingAdminId is not null && ActingAdminId > 0);
}
