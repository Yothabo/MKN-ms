
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MknMs.Application.Processes.Operations.AttendanceRuleEngine;
using Quartz;

namespace MknMs.Infrastructure.Jobs;

/// <summary>
/// Quartz job that runs process 13.0 Attendance Rule Engine on its
/// scheduled trigger.
/// </summary>
/// <remarks>
/// §13.6: the engine runs on a schedule and reads Attendance Rules,
/// evaluating each against the member population. The scheduled run
/// evaluates every enabled, non-Manual rule in ascending
/// AttendanceRuleId order.
///
/// The cadence is an implementation choice the specification does not
/// fix (backend/README.md, "Scheduling"). Daily at 04:00 in the
/// application timezone runs the engine after 5.0's 03:00 run and
/// before the day's 10.0 sweeps.
///
/// [DisallowConcurrentExecution] prevents Quartz from running two
/// instances at once. The manual endpoint is a separate path; the
/// service has no shared state across invocations, and the
/// evaluation order rule (ascending AttendanceRuleId) applies within
/// each run.
///
/// Specification: §13.6, §16.4.
/// </remarks>
[DisallowConcurrentExecution]
public sealed class AttendanceRuleEngineJob : IJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AttendanceRuleEngineJob> _logger;

    public AttendanceRuleEngineJob(
        IServiceProvider services,
        ILogger<AttendanceRuleEngineJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IAttendanceRuleEngineService>();

        var command = new AttendanceRuleEngineCommand
        {
            TriggerType = "Scheduled",
            RuleId = null,
            ActingAdminId = null,
        };

        var result = await service.RunAsync(command, context.CancellationToken);

        if (result.Status == "Success")
        {
            _logger.LogInformation(
                "Scheduled attendance-rule run completed: {RulesEvaluated} rules evaluated, {MembersEvaluated} members evaluated, {StatusChangesApplied} status changes applied, {NotificationsDispatched} notifications dispatched.",
                result.RulesEvaluated,
                result.MembersEvaluated,
                result.StatusChangesApplied,
                result.NotificationsDispatched);
        }
        else
        {
            _logger.LogError(
                "Scheduled attendance-rule run failed: {ErrorCode} {ErrorDetail}",
                result.ErrorCode,
                result.ErrorDetail);
        }
    }
}
