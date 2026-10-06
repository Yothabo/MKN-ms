namespace MknMs.Application.Processes.Configuration.ManageProgram;

/// <summary>
/// Subprocess of 8.0 — Manage Program.
/// </summary>
/// <remarks>
/// Creates, edits, and lists Program rows. Exactly one Program per
/// Event — enforced by the unique index uq_program_event on
/// (event_id). A Program has no IsActive flag; it is deactivated only
/// by deactivating its Event.
///
/// Specification: §4 (Program), §9.1.11.
/// </remarks>
public interface IManageProgramService
{
    Task<Program?> GetForEventAsync(int eventId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Program>> CreateAsync(
        int eventId,
        string title,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Program>> UpdateAsync(
        int programId,
        string title,
        CancellationToken cancellationToken = default);
}

public sealed class ManageProgramService : IManageProgramService
{
    private readonly MknDbContext _db;

    public ManageProgramService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<Program?> GetForEventAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _db.Programs.FirstOrDefaultAsync(p => p.EventId == eventId, cancellationToken);
    }

    public async Task<ConfigurationResult<Program>> CreateAsync(
        int eventId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(title))
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Title is required."));
        }

        var eventEntity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (eventEntity is null)
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Event does not exist."));
        }

        var existing = await _db.Programs
            .FirstOrDefaultAsync(p => p.EventId == eventId, cancellationToken);
        if (existing is not null)
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.DuplicateName,
                    "This event already has a Program. Only one Program per Event is permitted."));
        }

        var entity = new Program
        {
            EventId = eventId,
            Title = title.Trim(),
        };
        _db.Programs.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Program>.Success(entity);
    }

    public async Task<ConfigurationResult<Program>> UpdateAsync(
        int programId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(title))
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Title is required."));
        }

        var entity = await _db.Programs.FirstOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Program), programId);
        }

        entity.Title = title.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Program>.Success(entity);
    }
}
