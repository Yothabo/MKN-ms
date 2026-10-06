namespace MknMs.Application.Processes.Configuration.ConfigureServiceDefinition;

/// <summary>
/// Subprocess 1.5 — Configure Service Definition.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists ServiceDefinition rows.
/// OwningBranchId is optional: null means shared across all branches,
/// set means exclusive to one. The exclusivity rule is enforced later,
/// when a ServiceSchedule is created or edited — not here.
///
/// Specification: §4 (Service Definition), §9.1.4, §9.1.6.
/// </remarks>
public interface IConfigureServiceDefinitionService
{
    Task<IReadOnlyList<ServiceDefinition>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ServiceDefinition?> GetAsync(int serviceDefId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinition>> CreateAsync(
        string name,
        int serviceTypeId,
        int? owningBranchId = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinition>> UpdateAsync(
        int serviceDefId,
        string name,
        int serviceTypeId,
        int? owningBranchId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinition>> DeactivateAsync(
        int serviceDefId,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureServiceDefinitionService : IConfigureServiceDefinitionService
{
    private readonly MknDbContext _db;

    public ConfigureServiceDefinitionService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ServiceDefinition>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ServiceDefinitions.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }
        return await query.OrderBy(s => s.ServiceDefId).ToListAsync(cancellationToken);
    }

    public async Task<ServiceDefinition?> GetAsync(int serviceDefId, CancellationToken cancellationToken = default)
    {
        return await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
    }

    public async Task<ConfigurationResult<ServiceDefinition>> CreateAsync(
        string name,
        int serviceTypeId,
        int? owningBranchId = null,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var serviceType = await _db.ServiceTypes
            .FirstOrDefaultAsync(t => t.ServiceTypeId == serviceTypeId, cancellationToken);
        if (serviceType is null)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing, "ServiceType does not exist."));
        }

        if (owningBranchId is int branchId)
        {
            var branch = await _db.Branches
                .FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
            if (branch is null)
            {
                return ConfigurationResult<ServiceDefinition>.Failure(
                    new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing, "Owning branch does not exist."));
            }
        }

        var entity = new ServiceDefinition
        {
            Name = name.Trim(),
            ServiceTypeId = serviceTypeId,
            OwningBranchId = owningBranchId,
            IsActive = true,
        };
        _db.ServiceDefinitions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinition>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceDefinition>> UpdateAsync(
        int serviceDefId,
        string name,
        int serviceTypeId,
        int? owningBranchId,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceDefinition), serviceDefId);
        }

        var serviceType = await _db.ServiceTypes
            .FirstOrDefaultAsync(t => t.ServiceTypeId == serviceTypeId, cancellationToken);
        if (serviceType is null)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing, "ServiceType does not exist."));
        }

        if (owningBranchId is int branchId)
        {
            var branch = await _db.Branches
                .FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
            if (branch is null)
            {
                return ConfigurationResult<ServiceDefinition>.Failure(
                    new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing, "Owning branch does not exist."));
            }
        }

        entity.Name = name.Trim();
        entity.ServiceTypeId = serviceTypeId;
        entity.OwningBranchId = owningBranchId;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinition>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceDefinition>> DeactivateAsync(
        int serviceDefId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceDefinition), serviceDefId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Service definition is already inactive."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinition>.Success(entity);
    }
}
