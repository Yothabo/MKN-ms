
namespace MknMs.Application.Processes.Operations.AttendanceRuleEngine;

/// <summary>
/// The Attendance Rule Engine process (13.0).
/// </summary>
/// <remarks>
/// Reads Attendance Rules, evaluates each against the member
/// population, and applies the configured outcomes.
///
/// A scheduled run evaluates every enabled rule whose TriggerType is
/// not Manual, in ascending AttendanceRuleId order. Each rule's
/// outcome is applied before the next rule is evaluated. A manual
/// invocation evaluates exactly the named rule and no other.
///
/// Writes Member.MemberStatusId on SetStatus outcomes. Invokes 9.0
/// Dispatch Notification on Notify outcomes. Reads AttendanceRecord
/// and Readmission and never writes either.
///
/// Specification: §13.6; docs/processes/operations/13.0-attendance-rule-engine.md.
/// </remarks>
public interface IAttendanceRuleEngineService
{
    Task<AttendanceRuleEngineResult> RunAsync(
        AttendanceRuleEngineCommand command,
        CancellationToken cancellationToken = default);
}
