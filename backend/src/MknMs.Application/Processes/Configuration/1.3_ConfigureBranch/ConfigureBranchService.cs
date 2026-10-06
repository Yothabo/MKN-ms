namespace MknMs.Application.Processes.Configuration.ConfigureBranch;

/// <summary>
/// Subprocess 1.3 — Configure Branch.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists Branch rows. Location is a
/// single structured value stored as TEXT; its internal representation
/// is an open implementation choice.
///
/// Specification: §4 (Branch), §9.1.3, §9.1.1.
/// </remarks>
public interface IConfigureBranchService
{
    Task<IReadOnlyList<Branch>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<Branch?> GetAsync(int branchId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Branch>> CreateAsync(
        string name,
        string location,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Branch>> UpdateAsync(
        int branchId,
        string name,
        string location,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Branch>> DeactivateAsync(int branchId, CancellationToken cancellationToken = default);
}

public sealed class ConfigureBranchService : IConfigureBranchService
{
    private readonly MknDbContext _db;

    public ConfigureBranchService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Branch>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Branches.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(b => b.IsActive);
        }
        return await query.OrderBy(b => b.BranchId).ToListAsync(cancellationToken);
    }

    public async Task<Branch?> GetAsync(int branchId, CancellationToken cancellationToken = default)
    {
        return await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
    }

    public async Task<ConfigurationResult<Branch>> CreateAsync(
        string name,
        string location,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }
        if (!ConfigurationValidation.IsValidName(location))
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Location is required."));
        }

        var entity = new Branch { Name = name.Trim(), Location = location.Trim(), IsActive = true };
        _db.Branches.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Branch>.Success(entity);
    }

    public async Task<ConfigurationResult<Branch>> UpdateAsync(
        int branchId,
        string name,
        string location,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }
        if (!ConfigurationValidation.IsValidName(location))
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Location is required."));
        }

        var entity = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Branch), branchId);
        }

        entity.Name = name.Trim();
        entity.Location = location.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Branch>.Success(entity);
    }

    public async Task<ConfigurationResult<Branch>> DeactivateAsync(int branchId, CancellationToken cancellationToken = default)
    {
        var entity = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Branch), branchId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Branch is already inactive."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Branch>.Success(entity);
    }
}
