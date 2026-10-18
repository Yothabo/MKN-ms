namespace MknMs.Application.Processes.Configuration.Lookup_Capability;

/// <summary>
/// Read and create operations for the Capability lookup.
/// </summary>
/// <remarks>
/// Capability values are a D10 lookup — an open, administrator-defined
/// list of action names. The entity carries no reference to Role,
/// Admin, or PermissionTier; the assignment of capabilities to those
/// entities is deliberately deferred by the specification.
///
/// No soft delete, no deactivate. §9.7 describes Capability as
/// "removed; not soft-deleted independently, because nothing
/// references it", and the shape here matches ServiceTypeService —
/// create plus list, no lifecycle operation.
///
/// Specification: §4 (Capability), §9.1.12, §15.8.17.
/// </remarks>
public interface ICapabilityService
{
    Task<IReadOnlyList<Capability>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Capability>> CreateAsync(
        string name,
        string? description,
        CancellationToken cancellationToken = default);
}

public sealed class CapabilityService : ICapabilityService
{
    private readonly MknDbContext _db;

    public CapabilityService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Capability>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.Capabilities
            .Where(c => !c.IsDeleted)
            .OrderBy(c => c.CapabilityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<Capability>> CreateAsync(
        string name,
        string? description,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<Capability>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = new Capability
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim(),
        };

        _db.Capabilities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Capability>.Success(entity);
    }
}
