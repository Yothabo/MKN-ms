namespace MknMs.Application.Processes.Configuration.OccurrenceRead;

/// <summary>
/// Read-only access to ServiceOccurrence rows for the operator surface.
/// </summary>
/// <remarks>
/// A read surface, not a process. The process that creates occurrences
/// is 11.0 Materialize Occurrences (scheduled) and 8.0's Program Item
/// linkage (event-sourced). The processes that consume them are 5.0,
/// 6.0, 7.0, 10.0, and 12.0. None of them is here — this service exists
/// only so an operator can discover an OccurrenceId to pass to those
/// processes without querying the database directly.
///
/// There is no create, update, or delete path. Occurrences are written
/// by 11.0 and by 8.0's Program Item linkage; this service never writes.
///
/// §16.5 already names ServiceOccurrence as a read target of the system.
/// This service is the read surface that other processes already have
/// internally; it is exposed here so operators can reach it.
///
/// Specification: §4 (Service Occurrence), §16.5.
/// </remarks>
public interface IOccurrenceReadService
{
    Task<IReadOnlyList<ServiceOccurrence>> ListAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default);
}

public sealed class OccurrenceReadService : IOccurrenceReadService
{
    private readonly MknDbContext _db;

    public OccurrenceReadService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServiceOccurrence>> ListAsync(
        DateOnly? fromDate = null,
        DateOnly? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ServiceOccurrences.AsQueryable();

        if (fromDate is DateOnly from)
        {
            query = query.Where(o => o.Date >= from);
        }

        if (toDate is DateOnly to)
        {
            query = query.Where(o => o.Date <= to);
        }

        // Deterministic order: date, then occurrence id. Same ordering
        // the downstream processes use when they enumerate occurrences.
        return await query
            .OrderBy(o => o.Date)
            .ThenBy(o => o.OccurrenceId)
            .ToListAsync(cancellationToken);
    }
}
