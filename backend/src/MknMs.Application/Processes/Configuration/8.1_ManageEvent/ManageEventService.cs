namespace MknMs.Application.Processes.Configuration.ManageEvent;

/// <summary>
/// Subprocess of 8.0 — Manage Event.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists Event rows. Type and Location
/// are free text. May span multiple days.
///
/// Specification: §4 (Event), §9.1.11.
/// </remarks>
public interface IManageEventService
{
    Task<IReadOnlyList<Event>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Event?> GetAsync(int eventId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Event>> CreateAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string location,
        string type,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Event>> UpdateAsync(
        int eventId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string location,
        string type,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Event>> DeactivateAsync(
        int eventId,
        CancellationToken cancellationToken = default);
}

public sealed class ManageEventService : IManageEventService
{
    private readonly MknDbContext _db;

    public ManageEventService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Event>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Events.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }
        return await query.OrderBy(e => e.EventId).ToListAsync(cancellationToken);
    }

    public async Task<Event?> GetAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
    }

    public async Task<ConfigurationResult<Event>> CreateAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string location,
        string type,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(name, startDate, endDate, location, type);
        if (validation is not null)
        {
            return ConfigurationResult<Event>.Failure(validation);
        }

        var entity = new Event
        {
            Name = name.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Location = location.Trim(),
            Type = type.Trim(),
            IsActive = true,
        };
        _db.Events.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Event>.Success(entity);
    }

    public async Task<ConfigurationResult<Event>> UpdateAsync(
        int eventId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string location,
        string type,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(name, startDate, endDate, location, type);
        if (validation is not null)
        {
            return ConfigurationResult<Event>.Failure(validation);
        }

        var entity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Event), eventId);
        }

        entity.Name = name.Trim();
        entity.StartDate = startDate;
        entity.EndDate = endDate;
        entity.Location = location.Trim();
        entity.Type = type.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Event>.Success(entity);
    }

    public async Task<ConfigurationResult<Event>> DeactivateAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Event), eventId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<Event>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Event is already inactive."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Event>.Success(entity);
    }

    private static ConfigurationError? Validate(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string location,
        string type)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required.");
        }
        if (!ConfigurationValidation.IsValidName(location))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Location is required.");
        }
        if (!ConfigurationValidation.IsValidName(type))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Type is required.");
        }
        if (endDate < startDate)
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                "EndDate cannot be before StartDate.");
        }
        return null;
    }
}
