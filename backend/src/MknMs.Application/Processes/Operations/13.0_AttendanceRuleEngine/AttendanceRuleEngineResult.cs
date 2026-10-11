
namespace MknMs.Application.Processes.Operations.AttendanceRuleEngine;

/// <summary>
/// Summary of one Attendance Rule Engine run.
/// </summary>
/// <remarks>
/// The four counters describe what the run did. Status is Success
/// unless the run threw an exception or refused to run because a
/// required setting was missing.
///
/// ErrorCode and ErrorDetail are populated only on Failure. ErrorCode
/// carries one of the codes in docs/database/errors.md; ErrorDetail
/// is administrator-readable text.
///
/// Specification: §13.6; contracts.md "13.0 Attendance Rule Engine".
/// </remarks>
public sealed record AttendanceRuleEngineResult
{
    public required string Status { get; init; }

    public required int RulesEvaluated { get; init; }

    public required int MembersEvaluated { get; init; }

    public required int StatusChangesApplied { get; init; }

    public required int NotificationsDispatched { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorDetail { get; init; }
}
