namespace MknMs.Application.Processes.Configuration.Lookup_TimeOfDay;

/// <summary>
/// Read and create operations for the TimeOfDay lookup.
/// </summary>
/// <remarks>
/// TimeOfDay values are a D10 lookup — an open, administrator-defined
/// list. No deactivation flag exists for them; retiring a value means
/// not selecting it in future.
///
/// Specification: §4 (lookups), §9.1.3, §9.1.12.
/// </remarks>
public interface ITimeOfDayService
{
    Task<IReadOnlyList<TimeOfDay>> ListAsync(CancellationToken cancellationToken = default);

    Task<ConfigurationResult<TimeOfDay>> CreateAsync(
        string name,
        CancellationToken cancellationToken = default);
}

public sealed class TimeOfDayService : ITimeOfDayService
{
    private readonly MknDbContext _db;

    public TimeOfDayService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<TimeOfDay>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.TimeOfDays
            .OrderBy(t => t.TimeOfDayId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<TimeOfDay>> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<TimeOfDay>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = new TimeOfDay { Name = name.Trim() };
        _db.TimeOfDays.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<TimeOfDay>.Success(entity);
    }
}
