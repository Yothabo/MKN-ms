using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MknMs.Application.Processes.Operations.ManageConfirmation;
using Quartz;

namespace MknMs.Infrastructure.Jobs;

/// <summary>
/// Quartz job that runs the timeout sweep of process 7.0 Manage
/// Confirmation on its scheduled trigger.
/// </summary>
/// <remarks>
/// §11: 7.0 has two triggers — a member response and a scheduled check
/// "runs periodically to detect timed-out assignments." This job
/// supplies that scheduled check. The service reads the target status
/// from SystemSetting.TimedOutStatusID.
///
/// [DisallowConcurrentExecution] prevents Quartz from running two
/// instances of this job at once. A member response arriving during a
/// sweep is applied by the respond path, which is a separate
/// operation; the underlying rows are protected by the schema's
/// primary keys and the unique constraint on the assignment.
///
/// Specification: §11.
/// </remarks>
[DisallowConcurrentExecution]
public sealed class ManageConfirmationTimeoutJob : IJob
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ManageConfirmationTimeoutJob> _logger;

    public ManageConfirmationTimeoutJob(
        IServiceProvider services,
        ILogger<ManageConfirmationTimeoutJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        using var scope = _services.CreateScope();
        var service = scope.ServiceProvider
            .GetRequiredService<IManageConfirmationService>();

        var command = new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Sweep,
            AssignmentId = null,
        };

        var result = await service.RunAsync(command, context.CancellationToken);

        if (result.Status == "Success")
        {
            _logger.LogInformation(
                "Scheduled confirmation timeout sweep completed: {AssignmentsTransitioned} assignments transitioned, {ReplacementsCreated} replacements created, {SlotsLeftVacant} slots left vacant.",
                result.AssignmentsTransitioned,
                result.ReplacementsCreated,
                result.SlotsLeftVacant);
        }
        else
        {
            _logger.LogError(
                "Scheduled confirmation timeout sweep failed: {ErrorDetail}",
                result.ErrorDetail);
        }
    }
}
