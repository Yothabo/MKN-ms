using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MknMs.Application.Common;
using MknMs.Application.Processes.Operations.EvaluateFillStatus;
using MknMs.Persistence;
using Quartz;

namespace MknMs.Infrastructure.Jobs;

/// <summary>
/// Quartz job that runs the scheduled sweep of process 10.0 Evaluate
/// Fill Status.
/// </summary>
/// <remarks>
/// §12: 10.0 has two triggers — after assignment changes, and a
/// scheduled sweep "to catch occurrences whose state changed without a
/// direct trigger." This job supplies the scheduled sweep. The range
/// is bounded to the recent past and future in the configured
/// application timezone, so that an occurrence whose assignments
/// changed without firing the event path is re-evaluated.
///
/// The sweep is bounded to a window wide enough to cover any
/// occurrence whose state may have changed and narrow enough to keep
/// each scheduled run cheap. The window is an implementation choice
/// the specification does not fix.
///
/// Specification: §12.
/// </remarks>
[DisallowConcurrentExecution]
public sealed class EvaluateFillStatusSweepJob : IJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<EvaluateFillStatusSweepJob> _logger;

    public EvaluateFillStatusSweepJob(
        IServiceProvider services,
        ILogger<EvaluateFillStatusSweepJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IEvaluateFillStatusService>();
        var db = scope.ServiceProvider.GetRequiredService<MknDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var timeZone = await TimeZoneResolver.ResolveAsync(db, context.CancellationToken);
        var today = TimeZoneResolver.ToDateInTimeZone(clock.GetUtcNow(), timeZone);

        // Sweep the past 7 days and the next 30 days. The past window
        // covers occurrences whose assignments changed while an event
        // trigger was unavailable; the forward window covers upcoming
        // occurrences whose state may be evaluated in advance.
        var command = new EvaluateFillStatusCommand
        {
            OccurrenceId = null,
            FromDate = today.AddDays(-7),
            ToDate = today.AddDays(30),
        };

        var result = await service.RunAsync(command, context.CancellationToken);

        if (result.Status == "Success")
        {
            _logger.LogInformation(
                "Scheduled fill-status sweep completed: {OccurrencesEvaluated} evaluated, {UnfilledCount} unfilled, {PartiallyFilledCount} partially filled, {FilledCount} filled, {SkippedCancelledCount} skipped.",
                result.OccurrencesEvaluated,
                result.UnfilledCount,
                result.PartiallyFilledCount,
                result.FilledCount,
                result.SkippedCancelledCount);
        }
        else
        {
            _logger.LogError(
                "Scheduled fill-status sweep failed: {ErrorDetail}",
                result.ErrorDetail);
        }
    }
}
