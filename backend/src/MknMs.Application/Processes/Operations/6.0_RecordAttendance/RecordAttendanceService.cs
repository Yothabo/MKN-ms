namespace MknMs.Application.Processes.Operations.RecordAttendance;

/// <summary>
/// Implementation of process 6.0 Record Attendance.
/// </summary>
/// <remarks>
/// Reads Member, ServiceOccurrence, and the attendance register
/// scope, then writes AttendanceRecord. Nothing else. No
/// interpretation, no recency computation, no notification, no
/// trigger.
///
/// Two pre-write checks per §9.1.7 and §13:
///
///   - Attendance register eligibility. A member is tracked only if
///     they hold a ReceiptNumber or a CardNumber. When neither is
///     set, the operation is refused with ValidationFailed.
///   - Effective register scope. When the register is off at the
///     effective scope, the operation is refused with
///     ConfigurationMissing and no row is written.
///
/// Scope resolution per §9.6:
///
///   - Schedule-sourced occurrence: Branch.UsesAttendanceRegister of
///     the branch in the occurrence's time slot, then the global
///     AttendanceRegisterEnabled. Event does not participate.
///   - Event-sourced occurrence: Event.UsesAttendanceRegister, then
///     the global AttendanceRegisterEnabled. Branch does not
///     participate.
///
/// Idempotent per (MemberId, OccurrenceId): a repeat invocation
/// finds the existing row and returns it.
///
/// Specification: §13, §9.6, §9.1.7.
/// </remarks>
public sealed class RecordAttendanceService : IRecordAttendanceService
{
    private const string AttendanceRegisterEnabledKey = "AttendanceRegisterEnabled";

    private readonly MknDbContext _db;
    private readonly TimeProvider _clock;

    public RecordAttendanceService(MknDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<RecordAttendanceResult> RunAsync(
        RecordAttendanceCommand command,
        CancellationToken cancellationToken = default)
    {
        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.MemberId == command.MemberId, cancellationToken);

        if (member is null)
        {
            return Failure($"Member {command.MemberId} does not exist.");
        }

        var occurrence = await _db.ServiceOccurrences
            .FirstOrDefaultAsync(o => o.OccurrenceId == command.OccurrenceId, cancellationToken);

        if (occurrence is null)
        {
            return Failure($"ServiceOccurrence {command.OccurrenceId} does not exist.");
        }

        // §9.1.7: a member is tracked only if they hold a
        // ReceiptNumber or a CardNumber.
        if (string.IsNullOrWhiteSpace(member.ReceiptNumber)
            && string.IsNullOrWhiteSpace(member.CardNumber))
        {
            return Failure(
                $"Member {command.MemberId} does not hold a ReceiptNumber or CardNumber; "
                + "the member is not eligible for the attendance register.");
        }

        // §9.6: resolve the effective register scope.
        var globalSetting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == AttendanceRegisterEnabledKey,
                cancellationToken);

        if (globalSetting?.Value is null)
        {
            return Failure(
                $"Required setting '{AttendanceRegisterEnabledKey}' is not configured.");
        }

        if (!bool.TryParse(globalSetting.Value, out var globalRegisterEnabled))
        {
            return Failure(
                $"Setting '{AttendanceRegisterEnabledKey}' must be 'true' or 'false'.");
        }

        var effective = await ResolveEffectiveRegisterScopeAsync(
            occurrence, globalRegisterEnabled, cancellationToken);

        if (!effective)
        {
            return Failure(
                "The attendance register is off at the effective scope for this occurrence.");
        }

        // §13.1.2: idempotent per pair.
        var existing = await _db.AttendanceRecords
            .FirstOrDefaultAsync(a => a.MemberId == command.MemberId
                && a.OccurrenceId == command.OccurrenceId,
                cancellationToken);

        if (existing is not null)
        {
            return new RecordAttendanceResult
            {
                Status = "Success",
                Recorded = false,
                RecordId = existing.RecordId,
            };
        }

        var record = new AttendanceRecord
        {
            MemberId = command.MemberId,
            OccurrenceId = command.OccurrenceId,
            Timestamp = _clock.GetUtcNow(),
            Source = "Manual",
        };

        _db.AttendanceRecords.Add(record);

        try
        {
            await _db.SaveChangesAsync(cancellationToken);
        }
        catch (Npgsql.PostgresException ex) when (ex.SqlState == "23505")
        {
            // Unique violation on (member_id, occurrence_id). This is
            // the concurrent-duplicate case: another writer inserted
            // the same pair between our existence check and this
            // insert. §13.1.2 requires idempotent success. Detach
            // the failed entity, reload the existing row, return it.
            _db.ChangeTracker.Clear();

            var concurrent = await _db.AttendanceRecords
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.MemberId == command.MemberId
                    && a.OccurrenceId == command.OccurrenceId,
                    cancellationToken);

            if (concurrent is not null)
            {
                return new RecordAttendanceResult
                {
                    Status = "Success",
                    Recorded = false,
                    RecordId = concurrent.RecordId,
                };
            }

            throw;
        }

        return new RecordAttendanceResult
        {
            Status = "Success",
            Recorded = true,
            RecordId = record.RecordId,
        };
    }

    private async Task<bool> ResolveEffectiveRegisterScopeAsync(
        ServiceOccurrence occurrence,
        bool globalRegisterEnabled,
        CancellationToken cancellationToken)
    {
        // Schedule-sourced: branch override, then global.
        if (occurrence.ScheduleId is int scheduleId)
        {
            var branchOverride = await _db.ServiceSchedules
                .Where(s => s.ScheduleId == scheduleId)
                .Select(s => s.TimeSlot.Branch.UsesAttendanceRegister)
                .FirstOrDefaultAsync(cancellationToken);

            return branchOverride ?? globalRegisterEnabled;
        }

        // Event-sourced: event override, then global.
        if (occurrence.EventId is int eventId)
        {
            var eventOverride = await _db.Events
                .Where(e => e.EventId == eventId)
                .Select(e => e.UsesAttendanceRegister)
                .FirstOrDefaultAsync(cancellationToken);

            return eventOverride ?? globalRegisterEnabled;
        }

        // An occurrence with neither source violates B18; but the
        // check constraint forbids it, and the code does not add
        // behaviour to handle an impossible state. Fall back to the
        // global setting.
        return globalRegisterEnabled;
    }

    private static RecordAttendanceResult Failure(string detail) =>
        new()
        {
            Status = "Failure",
            Recorded = false,
            RecordId = 0,
            ErrorDetail = detail,
        };
}
