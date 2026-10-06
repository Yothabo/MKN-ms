namespace MknMs.Application.Processes.Operations.RecordAttendance;

/// <summary>
/// Implementation of process 6.0 Record Attendance.
/// </summary>
/// <remarks>
/// The narrowest process in the system. Reads Member and
/// ServiceOccurrence to validate the pair, writes AttendanceRecord.
/// Nothing else. No interpretation, no recency computation, no
/// notification, no trigger.
///
/// Idempotent per (MemberId, OccurrenceId): a repeat invocation finds
/// the existing row and returns it, rather than failing or creating a
/// duplicate.
///
/// Specification: §13.
/// </remarks>
public sealed class RecordAttendanceService : IRecordAttendanceService
{
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
        try
        {
            // Validate the pair's referents exist. §13 requires the
            // member and the occurrence to be real rows; nothing else
            // about either is consulted.
            var memberExists = await _db.Members
                .AnyAsync(m => m.MemberId == command.MemberId, cancellationToken);

            if (!memberExists)
            {
                return new RecordAttendanceResult
                {
                    Status = "Failure",
                    Recorded = false,
                    RecordId = 0,
                    ErrorDetail = $"Member {command.MemberId} does not exist.",
                };
            }

            var occurrenceExists = await _db.ServiceOccurrences
                .AnyAsync(o => o.OccurrenceId == command.OccurrenceId, cancellationToken);

            if (!occurrenceExists)
            {
                return new RecordAttendanceResult
                {
                    Status = "Failure",
                    Recorded = false,
                    RecordId = 0,
                    ErrorDetail = $"ServiceOccurrence {command.OccurrenceId} does not exist.",
                };
            }

            // Idempotency: existing row for this pair is returned
            // unchanged. §13.1.2.
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
            };

            _db.AttendanceRecords.Add(record);
            await _db.SaveChangesAsync(cancellationToken);

            return new RecordAttendanceResult
            {
                Status = "Success",
                Recorded = true,
                RecordId = record.RecordId,
            };
        }
        catch (Exception ex)
        {
            return new RecordAttendanceResult
            {
                Status = "Failure",
                Recorded = false,
                RecordId = 0,
                ErrorDetail = ex.Message,
            };
        }
    }
}
