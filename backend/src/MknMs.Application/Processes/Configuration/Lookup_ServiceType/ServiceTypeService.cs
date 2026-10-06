namespace MknMs.Application.Processes.Configuration.Lookup_ServiceType;

/// <summary>
/// Read and create operations for the ServiceType lookup.
/// </summary>
/// <remarks>
/// ServiceType values are a D10 lookup — an open, administrator-defined
/// list.
///
/// Specification: §4 (lookups), §9.1.4, §9.1.12.
/// </remarks>
public interface IServiceTypeService
{
    Task<IReadOnlyList<ServiceType>> ListAsync(CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceType>> CreateAsync(
        string name,
        CancellationToken cancellationToken = default);
}

public sealed class ServiceTypeService : IServiceTypeService
{
    private readonly MknDbContext _db;

    public ServiceTypeService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServiceType>> ListAsync(CancellationToken cancellationToken = default)
    {
        return await _db.ServiceTypes
            .OrderBy(t => t.ServiceTypeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<ServiceType>> CreateAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<ServiceType>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = new ServiceType { Name = name.Trim() };
        _db.ServiceTypes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceType>.Success(entity);
    }
}
