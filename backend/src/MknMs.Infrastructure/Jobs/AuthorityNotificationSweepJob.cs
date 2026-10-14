
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MknMs.Application.Processes.Operations.DispatchNotification;
using Quartz;

namespace MknMs.Infrastructure.Jobs;

/// <summary>
/// Quartz job that runs 9.0 Path 2, the authority-notification
/// sweep.
/// </summary>
/// <remarks>
/// §16.4: Path 2 is a scheduled trigger of 9.0, not an invocation.
/// The scheduler starts 9.0 directly; no process calls it. This job
/// is that scheduled trigger.
///
/// The sweep reads ConfigurationAuditLog rows whose NotifiedAt is
/// null, resolves recipients through NotificationSubscription, and
/// dispatches. [DisallowConcurrentExecution] prevents two instances
/// of this job from running at once, so a slow sweep does not race
/// itself and produce duplicate dispatches to a row that is already
/// being processed.
///
/// Specification: §14.1.3, §14.1.4, §14.1.9, §16.4.
/// </remarks>
[DisallowConcurrentExecution]
public sealed class AuthorityNotificationSweepJob : IJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<AuthorityNotificationSweepJob> _logger;

    public AuthorityNotificationSweepJob(
        IServiceProvider services,
        ILogger<AuthorityNotificationSweepJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IAuthorityNotificationSweepService>();

        var result = await service.RunAsync(context.CancellationToken);

        if (result.Status == "Success")
        {
            _logger.LogInformation(
                "Authority-notification sweep completed: {AuditRowsProcessed} audit row(s) processed, {NotificationsDispatched} notification(s) dispatched, {RowsLeftForRetry} row(s) left for retry.",
                result.AuditRowsProcessed,
                result.NotificationsDispatched,
                result.RowsLeftForRetry);
        }
        else
        {
            _logger.LogError(
                "Authority-notification sweep failed: {ErrorCode} {ErrorDetail}",
                result.ErrorCode,
                result.ErrorDetail);
        }
    }
}
