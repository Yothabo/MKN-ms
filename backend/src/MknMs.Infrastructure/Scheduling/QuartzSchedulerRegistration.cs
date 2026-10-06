using Microsoft.Extensions.DependencyInjection;
using MknMs.Infrastructure.Jobs;
using Quartz;

namespace MknMs.Infrastructure.Scheduling;

/// <summary>
/// Registers Quartz.NET with the scheduled jobs the specification
/// defines.
/// </summary>
/// <remarks>
/// The scheduler's trigger timezone is the ApplicationTimeZone setting,
/// resolved by the caller and passed in here. Every trigger uses
/// .InTimeZone(timeZone) so that "02:00" means 02:00 in the configured
/// application timezone, not in the host's local timezone. This keeps
/// the process's view of "today" and the scheduler's trigger timezone
/// derived from the same configuration value.
///
/// Four processes have a scheduled trigger per §16's trigger summary:
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
    /// <summary>
    /// The cron expressions the scheduler uses. Exposed as constants so
    /// a test can assert the registered triggers match them without
    /// duplicating the strings.
    /// </summary>
    public const string MaterializeCron = "0 0 2 * * ?";
    public const string GenerateCron = "0 0 3 * * ?";
    public const string ConfirmationTimeoutCron = "0 0 * * * ?";
    public const string FillStatusSweepCron = "0 30 * * * ?";

    public static IServiceCollection AddMknScheduledJobs(
        this IServiceCollection services,
        TimeZoneInfo timeZone)
    {
        services.AddQuartz(q =>
        {
            // 11.0 Materialize Occurrences — daily at 02:00.
            var materializeKey = new JobKey(nameof(MaterializeOccurrencesJob));
            q.AddJob<MaterializeOccurrencesJob>(opts => opts.WithIdentity(materializeKey));
            q.AddTrigger(opts => opts
                .ForJob(materializeKey)
                .WithIdentity($"{nameof(MaterializeOccurrencesJob)}-trigger")
                .WithCronSchedule(MaterializeCron, cron => cron.InTimeZone(timeZone)));

            // 5.0 Generate Assignment — daily at 03:00.
            var generateKey = new JobKey(nameof(GenerateAssignmentJob));
            q.AddJob<GenerateAssignmentJob>(opts => opts.WithIdentity(generateKey));
            q.AddTrigger(opts => opts
                .ForJob(generateKey)
                .WithIdentity($"{nameof(GenerateAssignmentJob)}-trigger")
                .WithCronSchedule(GenerateCron, cron => cron.InTimeZone(timeZone)));

            // 7.0 Manage Confirmation timeout sweep — hourly.
            var confirmationKey = new JobKey(nameof(ManageConfirmationTimeoutJob));
            q.AddJob<ManageConfirmationTimeoutJob>(opts => opts.WithIdentity(confirmationKey));
            q.AddTrigger(opts => opts
                .ForJob(confirmationKey)
                .WithIdentity($"{nameof(ManageConfirmationTimeoutJob)}-trigger")
                .WithCronSchedule(ConfirmationTimeoutCron, cron => cron.InTimeZone(timeZone)));

            // 10.0 Evaluate Fill Status sweep — hourly, offset.
            var fillKey = new JobKey(nameof(EvaluateFillStatusSweepJob));
            q.AddJob<EvaluateFillStatusSweepJob>(opts => opts.WithIdentity(fillKey));
            q.AddTrigger(opts => opts
                .ForJob(fillKey)
                .WithIdentity($"{nameof(EvaluateFillStatusSweepJob)}-trigger")
                .WithCronSchedule(FillStatusSweepCron, cron => cron.InTimeZone(timeZone)));
        });

        services.AddQuartzHostedService(opts =>
        {
            opts.WaitForJobsToComplete = true;
        });

        return services;
    }
}
