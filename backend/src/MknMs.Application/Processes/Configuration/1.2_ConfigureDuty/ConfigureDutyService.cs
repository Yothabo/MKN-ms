namespace MknMs.Application.Processes.Configuration.ConfigureDuty;

/// <summary>
/// Subprocess 1.2 — Configure Duty.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists Duty rows. A Duty is a name.
/// It carries no reference to Role, no restriction, no priority.
///
/// Specification: §4 (Duty), §9.1.2, §9.1.1.
/// </remarks>
public interface IConfigureDutyService
{
    Task<IReadOnlyList<Duty>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<Duty?> GetAsync(int dutyId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Duty>> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Duty>> RenameAsync(int dutyId, string newName, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Duty>> DeactivateAsync(int dutyId, CancellationToken cancellationToken = default);
}

public sealed class ConfigureDutyService : IConfigureDutyService
{
    private readonly MknDbContext _db;

    public ConfigureDutyService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Duty>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Duties.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }
        return await query.OrderBy(d => d.DutyId).ToListAsync(cancellationToken);
    }

    public async Task<Duty?> GetAsync(int dutyId, CancellationToken cancellationToken = default)
    {
        return await _db.Duties.FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
    }

    public async Task<ConfigurationResult<Duty>> CreateAsync(string name, CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = new Duty { Name = name.Trim(), IsActive = true };
        _db.Duties.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Duty>.Success(entity);
    }

    public async Task<ConfigurationResult<Duty>> RenameAsync(int dutyId, string newName, CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(newName))
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = await _db.Duties.FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Duty), dutyId);
        }

        entity.Name = newName.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Duty>.Success(entity);
    }

    public async Task<ConfigurationResult<Duty>> DeactivateAsync(int dutyId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Duties.FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Duty), dutyId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Duty is already inactive."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Duty>.Success(entity);
    }
}
