using Microsoft.Extensions.DependencyInjection;
using MknMs.Infrastructure.Jobs;
using Quartz;

namespace MknMs.Infrastructure.Scheduling;

/// <summary>
/// Registers Quartz.NET with the scheduled jobs the specification
/// defines.
/// </summary>
/// <remarks>
/// Four processes have a scheduled trigger per §16's trigger summary:
/// 11.0 (daily), 5.0 (recurring), 7.0 (periodic timeout check), and
/// 10.0 (periodic sweep). The cadences the specification fixes are:
///
///   - 11.0: daily. §7 says "daily, a fixed frequency, not
///     administrator-configurable." The hour is an implementation
///     choice; 02:00 in the application timezone runs the pipeline
///     before the day begins.
///   - 5.0: §10 says "a recurring run." Daily at 03:00, one hour after
///     materialization, fills the occurrences that were just created.
///   - 7.0: §11 says the check "runs periodically." Hourly.
///   - 10.0: §12 says the sweep runs "periodically." Hourly.
///
/// All four use [DisallowConcurrentExecution] on their job classes so
/// Quartz never runs two instances of the same job at once.
///
/// Specification: §7, §10, §11, §12, §16.
/// </remarks>
public static class QuartzSchedulerRegistration
{
    public static IServiceCollection AddMknScheduledJobs(this IServiceCollection services)
    {
        services.AddQuartz(q =>
        {
            // 11.0 Materialize Occurrences — daily at 02:00.
            var materializeKey = new JobKey(nameof(MaterializeOccurrencesJob));
            q.AddJob<MaterializeOccurrencesJob>(opts => opts.WithIdentity(materializeKey));
            q.AddTrigger(opts => opts
                .ForJob(materializeKey)
                .WithIdentity($"{nameof(MaterializeOccurrencesJob)}-trigger")
                .WithCronSchedule("0 0 2 * * ?"));

            // 5.0 Generate Assignment — daily at 03:00.
            var generateKey = new JobKey(nameof(GenerateAssignmentJob));
            q.AddJob<GenerateAssignmentJob>(opts => opts.WithIdentity(generateKey));
            q.AddTrigger(opts => opts
                .ForJob(generateKey)
                .WithIdentity($"{nameof(GenerateAssignmentJob)}-trigger")
                .WithCronSchedule("0 0 3 * * ?"));

            // 7.0 Manage Confirmation timeout sweep — hourly.
            var confirmationKey = new JobKey(nameof(ManageConfirmationTimeoutJob));
            q.AddJob<ManageConfirmationTimeoutJob>(opts => opts.WithIdentity(confirmationKey));
            q.AddTrigger(opts => opts
                .ForJob(confirmationKey)
                .WithIdentity($"{nameof(ManageConfirmationTimeoutJob)}-trigger")
                .WithCronSchedule("0 0 * * * ?"));

            // 10.0 Evaluate Fill Status sweep — hourly, offset.
            var fillKey = new JobKey(nameof(EvaluateFillStatusSweepJob));
            q.AddJob<EvaluateFillStatusSweepJob>(opts => opts.WithIdentity(fillKey));
            q.AddTrigger(opts => opts
                .ForJob(fillKey)
                .WithIdentity($"{nameof(EvaluateFillStatusSweepJob)}-trigger")
                .WithCronSchedule("0 30 * * * ?"));
        });

        services.AddQuartzHostedService(opts =>
        {
            opts.WaitForJobsToComplete = true;
        });

        return services;
    }
}
