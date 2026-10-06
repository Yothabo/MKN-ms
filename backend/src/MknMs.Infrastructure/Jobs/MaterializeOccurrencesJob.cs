using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MknMs.Application.Processes.Operations.MaterializeOccurrences;
using Quartz;

namespace MknMs.Infrastructure.Jobs;

/// <summary>
/// Quartz job that runs process 11.0 Materialize Occurrences on its
/// scheduled trigger.
/// </summary>
/// <remarks>
/// §7: 11.0 runs as part of the application's existing scheduled-job
/// mechanism, daily, at a fixed frequency, not administrator-
/// configurable. Overlapping runs are not allowed. This job uses
/// [DisallowConcurrentExecution] so that Quartz never runs two
/// instances of it at once within the scheduler. The manual endpoint
/// is a separate trigger path; the service's own advisory lock
/// (see MaterializeOccurrencesService) covers the case where the
/// manual trigger and the scheduled trigger would otherwise overlap.
///
/// The job resolves IMaterializeOccurrencesService from the job
/// context's service provider so that the scoped service sees a scoped
/// DbContext — the standard Quartz.Extensions.Hosting pattern.
///
/// Specification: §7, §8.
/// </remarks>
[DisallowConcurrentExecution]
public sealed class MaterializeOccurrencesJob : IJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<MaterializeOccurrencesJob> _logger;

    public MaterializeOccurrencesJob(
        IServiceProvider services,
        ILogger<MaterializeOccurrencesJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IMaterializeOccurrencesService>();

        var command = new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
            TriggeredByAdminId = null,
            HorizonDaysOverride = null,
        };

        var result = await service.RunAsync(command, context.CancellationToken);

        if (result.Status == "Success")
        {
            _logger.LogInformation(
                "Scheduled materialize run {RunId} completed: {SchedulesEvaluated} schedules evaluated, {OccurrencesCreated} occurrences created.",
                result.RunId,
                result.SchedulesEvaluated,
                result.OccurrencesCreated);
        }
        else
        {
            _logger.LogError(
                "Scheduled materialize run {RunId} failed: {ErrorDetail}",
                result.RunId,
                result.ErrorDetail);
        }
    }
}
