using System.Data;
using MknMs.Domain.D11_SystemSetting;

namespace MknMs.Application.Processes.Operations.MaterializeOccurrences;

/// <summary>
/// Implementation of process 11.0 Materialize Occurrences.
/// </summary>
/// <remarks>
/// Additive and idempotent. Reads active schedules, computes candidate
/// dates within the horizon, and inserts missing occurrences. Never
/// modifies or deletes existing rows.
///
/// §7: "Overlapping runs are not allowed." This applies to both the
/// scheduled trigger and the manual trigger. The
/// [DisallowConcurrentExecution] attribute on the Quartz job covers
/// the scheduled path; a PostgreSQL session-level advisory lock taken
/// here covers both paths, so that a manual trigger fired while a
/// scheduled run is in progress does not overlap it. If the lock
/// cannot be acquired, the run returns a Failure result with
/// "another materializer run is in progress" and does nothing else.
///
/// Specification: §7, §8.
/// </remarks>
public sealed class MaterializeOccurrencesService : IMaterializeOccurrencesService
{
    private const string HorizonSettingKey = "OccurrenceHorizonDays";

    /// <summary>
    /// The PostgreSQL advisory lock key identifying the materializer's
    /// mutual-exclusion lock. An arbitrary constant, chosen to be clear
    /// of any identity sequence. It is not exposed as configuration.
    /// </summary>
    private const long MaterializerLockKey = 1100;

    private readonly MknDbContext _db;
    private readonly TimeProvider _clock;

    public MaterializeOccurrencesService(MknDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<MaterializeOccurrencesResult> RunAsync(
        MaterializeOccurrencesCommand command,
        CancellationToken cancellationToken = default)
    {
        ValidateCommand(command);

        // Acquire the advisory lock. Session-scoped: held across every
        // transaction this method opens, released in the finally below.
        var connection = _db.Database.GetDbConnection();
        var wasClosed = connection.State != ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        var lockAcquired = false;

        try
        {
            await using (var lockCommand = connection.CreateCommand())
            {
                lockCommand.CommandText = "SELECT pg_try_advisory_lock(@key)";
                var parameter = lockCommand.CreateParameter();
                parameter.ParameterName = "@key";
                parameter.Value = MaterializerLockKey;
                lockCommand.Parameters.Add(parameter);

                var result = await lockCommand.ExecuteScalarAsync(cancellationToken);
                lockAcquired = result is bool acquired && acquired;
            }

            if (!lockAcquired)
            {
                return new MaterializeOccurrencesResult
                {
                    RunId = 0,
                    Status = "Failure",
                    SchedulesEvaluated = 0,
                    OccurrencesCreated = 0,
                    ErrorDetail = "Another materializer run is in progress.",
                };
            }

            return await ExecuteRunAsync(command, cancellationToken);
        }
        finally
        {
            if (lockAcquired)
            {
                try
                {
                    await using var unlockCommand = connection.CreateCommand();
                    unlockCommand.CommandText = "SELECT pg_advisory_unlock(@key)";
                    var parameter = unlockCommand.CreateParameter();
                    parameter.ParameterName = "@key";
                    parameter.Value = MaterializerLockKey;
                    unlockCommand.Parameters.Add(parameter);
                    await unlockCommand.ExecuteScalarAsync(CancellationToken.None);
                }
                catch
                {
                    // Best-effort release. If the unlock fails, the lock
                    // is released when the connection closes; closing
                    // the connection is the next line.
                }
            }

            if (wasClosed && connection.State == ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private async Task<MaterializeOccurrencesResult> ExecuteRunAsync(
        MaterializeOccurrencesCommand command,
        CancellationToken cancellationToken)
    {
        var startedAt = _clock.GetUtcNow();
        var run = new MaterializerRun
        {
            TriggerType = command.TriggerType,
            TriggeredBy = command.TriggeredByAdminId,
            StartedAt = startedAt,
            SchedulesEvaluated = 0,
            OccurrencesCreated = 0,
            Status = "Success",
        };

        try
        {
            var horizonDays = await ResolveHorizonAsync(command, cancellationToken);
            var timeZone = await TimeZoneResolver.ResolveAsync(_db, cancellationToken);
            var today = TimeZoneResolver.ToDateInTimeZone(_clock.GetUtcNow(), timeZone);
            var targetEnd = today.AddDays(horizonDays);

            var activeSchedules = await _db.ServiceSchedules
                .Where(s => s.IsActive
                    && s.ServiceDefinition.IsActive
                    && s.TimeSlot.IsActive)
                .Select(s => new ScheduleForMaterialization
                {
                    ScheduleId = s.ScheduleId,
                    DayOfWeek = s.TimeSlot.DayOfWeek,
                    StartTime = s.StartTime,
                    ServiceTypeId = s.ServiceDefinition.ServiceTypeId,
                })
                .ToListAsync(cancellationToken);

            run.SchedulesEvaluated = activeSchedules.Count;

            var existingOccurrences = await _db.ServiceOccurrences
                .Where(o => o.ScheduleId != null
                    && o.Date >= today
                    && o.Date <= targetEnd)
                .Select(o => new { o.ScheduleId, o.Date })
                .ToListAsync(cancellationToken);

            var existingSet = existingOccurrences
                .Select(x => (x.ScheduleId!.Value, x.Date))
                .ToHashSet();

            var createdCount = 0;

            foreach (var schedule in activeSchedules)
            {
                var targetDay = ParseDayOfWeek(schedule.DayOfWeek);

                for (var date = today; date <= targetEnd; date = date.AddDays(1))
                {
                    if (date.DayOfWeek != targetDay)
                    {
                        continue;
                    }

                    if (existingSet.Contains((schedule.ScheduleId, date)))
                    {
                        continue;
                    }

                    var occurrence = new ServiceOccurrence
                    {
                        ScheduleId = schedule.ScheduleId,
                        EventId = null,
                        Date = date,
                        ServiceTypeId = schedule.ServiceTypeId,
                        StartTime = schedule.StartTime,
                        FillStatusId = null,
                        GeneratedBy = "System",
                        CreatedBy = null,
                        ChangedBy = null,
                    };

                    _db.ServiceOccurrences.Add(occurrence);
                    await _db.SaveChangesAsync(cancellationToken);
                    createdCount++;
                }
            }

            run.OccurrencesCreated = createdCount;
            run.CompletedAt = _clock.GetUtcNow();
            run.Status = "Success";

            _db.MaterializerRuns.Add(run);
            await _db.SaveChangesAsync(cancellationToken);

            return new MaterializeOccurrencesResult
            {
                RunId = run.RunId,
                Status = "Success",
                SchedulesEvaluated = run.SchedulesEvaluated,
                OccurrencesCreated = run.OccurrencesCreated,
            };
        }
        catch (Exception ex)
        {
            run.CompletedAt = _clock.GetUtcNow();
            run.Status = "Failure";
            run.ErrorDetail = ex.Message;

            _db.MaterializerRuns.Add(run);

            try
            {
                await _db.SaveChangesAsync(cancellationToken);
            }
            catch
            {
                // If even the failure record cannot be written, the
                // exception is returned below. Nothing further can be
                // done from this layer.
            }

            return new MaterializeOccurrencesResult
            {
                RunId = run.RunId,
                Status = "Failure",
                SchedulesEvaluated = run.SchedulesEvaluated,
                OccurrencesCreated = run.OccurrencesCreated,
                ErrorDetail = run.ErrorDetail,
            };
        }
    }

    private static void ValidateCommand(MaterializeOccurrencesCommand command)
    {
        switch (command.TriggerType)
        {
            case "Manual" when command.TriggeredByAdminId is null:
                throw new InvalidOperationException(
                    "A Manual materialize run requires TriggeredByAdminId.");

            case "Scheduled" when command.TriggeredByAdminId is not null:
                throw new InvalidOperationException(
                    "A Scheduled materialize run must have TriggeredByAdminId = null.");

            case "Manual" or "Scheduled":
                return;

            default:
                throw new InvalidOperationException(
                    $"Unknown trigger type '{command.TriggerType}'. " +
                    "Expected 'Scheduled' or 'Manual'.");
        }
    }

    private async Task<int> ResolveHorizonAsync(
        MaterializeOccurrencesCommand command,
        CancellationToken cancellationToken)
    {
        if (command.HorizonDaysOverride is int overrideDays)
        {
            if (overrideDays < 0)
            {
                throw new InvalidOperationException(
                    "HorizonDaysOverride must be zero or greater.");
            }

            return overrideDays;
        }

        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == HorizonSettingKey, cancellationToken);

        if (setting is null || setting.Value is null)
        {
            if (setting?.Required ?? true)
            {
                throw new InvalidOperationException(
                    $"Required setting '{HorizonSettingKey}' is not configured.");
            }

            throw new InvalidOperationException(
                $"Setting '{HorizonSettingKey}' has no value and no override was supplied.");
        }

        if (!int.TryParse(setting.Value, out var days))
        {
            throw new InvalidOperationException(
                $"Setting '{HorizonSettingKey}' has non-integer value '{setting.Value}'.");
        }

        if (days < 0)
        {
            throw new InvalidOperationException(
                $"Setting '{HorizonSettingKey}' must be zero or greater.");
        }

        return days;
    }

    private static DayOfWeek ParseDayOfWeek(string value)
    {
        if (!Enum.TryParse<DayOfWeek>(value, ignoreCase: true, out var result))
        {
            throw new InvalidOperationException(
                $"Unrecognized day-of-week value '{value}'.");
        }

        return result;
    }

    private sealed class ScheduleForMaterialization
    {
        public int ScheduleId { get; init; }
        public string DayOfWeek { get; init; } = null!;
        public TimeOnly StartTime { get; init; }
        public int ServiceTypeId { get; init; }
    }
}
