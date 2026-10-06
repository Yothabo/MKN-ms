using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MknMs.Application.Common;
using MknMs.Application.Processes.Operations.GenerateAssignment;
using MknMs.Persistence;
using Quartz;

namespace MknMs.Infrastructure.Jobs;

/// <summary>
/// Quartz job that runs process 5.0 Generate Assignment on its
/// scheduled trigger.
/// </summary>
/// <remarks>
/// §10: 5.0 has two triggers — a scheduled run and a manual
/// administrator action. The scheduled run "picks up any occurrence
/// not yet filled, or any occurrence whose assignments have become
/// incomplete." This job supplies that scheduled trigger with the
/// command shape it requires: a date range, bounded to today's
/// occurrences in the configured application timezone.
///
/// The date is derived using TimeZoneResolver, the same resolver
/// process 5.0 itself uses, so "today" means the same thing to the
/// job and to the service.
///
/// [DisallowConcurrentExecution] prevents Quartz from running two
/// instances of this job at once. The manual endpoint is a separate
/// path; the service is additive and re-entrant, so a manual trigger
/// overlapping a scheduled one produces no duplicate data.
///
/// Specification: §10.
/// </remarks>
[DisallowConcurrentExecution]
public sealed class GenerateAssignmentJob : IJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<GenerateAssignmentJob> _logger;

    public GenerateAssignmentJob(
        IServiceProvider services,
        ILogger<GenerateAssignmentJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IGenerateAssignmentService>();
        var db = scope.ServiceProvider.GetRequiredService<MknDbContext>();
        var clock = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        var timeZone = await TimeZoneResolver.ResolveAsync(db, context.CancellationToken);
        var today = TimeZoneResolver.ToDateInTimeZone(clock.GetUtcNow(), timeZone);

        var command = new GenerateAssignmentCommand
        {
            OccurrenceId = null,
            FromDate = today,
            ToDate = today,
        };

        var result = await service.RunAsync(command, context.CancellationToken);

        if (result.Status == "Success")
        {
            _logger.LogInformation(
                "Scheduled generate-assignment run completed for {Date}: {OccurrencesProcessed} occurrences processed, {AssignmentsCreated} assignments created.",
                today,
                result.OccurrencesProcessed,
                result.AssignmentsCreated);
        }
        else
        {
            _logger.LogError(
                "Scheduled generate-assignment run failed: {ErrorDetail}",
                result.ErrorDetail);
        }
    }
}
