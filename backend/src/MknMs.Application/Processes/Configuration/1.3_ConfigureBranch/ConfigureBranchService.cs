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
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Branch>> UpdateAsync(
        int branchId,
        string name,
        string location,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Branch>> DeactivateAsync(
        int branchId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Branch>> SoftDeleteAsync(
        int branchId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureBranchService : IConfigureBranchService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ConfigureBranchService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
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
        int actingAdminId,
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
        int actingAdminId,
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

    public async Task<ConfigurationResult<Branch>> DeactivateAsync(
        int branchId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
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

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Branch),
            EntityId = entity.BranchId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Branch>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Branch>.Success(entity);
    }

    public async Task<ConfigurationResult<Branch>> SoftDeleteAsync(
        int branchId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.Branches
            .FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Branch), branchId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Branch is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(Branch), branchId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<Branch>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<Branch>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Branch),
            EntityId = entity.BranchId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Branch>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Branch>.Success(entity);
    }
}
