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
        int actingAdminId,
        int? owningBranchId = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinition>> UpdateAsync(
        int serviceDefId,
        string name,
        int serviceTypeId,
        int? owningBranchId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinition>> ReactivateAsync(
        int serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinition>> DeactivateAsync(
        int serviceDefId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinition>> SoftDeleteAsync(
        int serviceDefId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureServiceDefinitionService : IConfigureServiceDefinitionService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ConfigureServiceDefinitionService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<IReadOnlyList<ServiceDefinition>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ServiceDefinitions.Where(s => !s.IsDeleted);
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }
        return await query.OrderBy(s => s.ServiceDefId).ToListAsync(cancellationToken);
    }

    public async Task<ServiceDefinition?> GetAsync(int serviceDefId, CancellationToken cancellationToken = default)
    {
        return await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId && !s.IsDeleted, cancellationToken);
    }

    public async Task<ConfigurationResult<ServiceDefinition>> CreateAsync(
        string name,
        int serviceTypeId,
        int actingAdminId,
        int? owningBranchId = null,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(adminError);
        }

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
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(adminError);
        }

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

        // §9.1.6 / B19: changing OwningBranchId to a non-null value
        // must not break an existing schedule. Every schedule currently
        // attached must have a time slot at the new owning branch.
        if (owningBranchId is int newOwningBranchId
            && entity.OwningBranchId != newOwningBranchId)
        {
            var mismatchCount = await _db.ServiceSchedules
                .Where(s => s.ServiceDefId == serviceDefId)
                .Where(s => s.TimeSlot.BranchId != newOwningBranchId)
                .CountAsync(cancellationToken);

            if (mismatchCount > 0)
            {
                return ConfigurationResult<ServiceDefinition>.Failure(
                    new ConfigurationError(
                        ConfigurationErrorCodes.InvalidValue,
                        $"{mismatchCount} Service Schedule(s) for this definition "
                        + "use a time slot at a different branch. Deactivate them "
                        + "before changing the owning branch."));
            }
        }

        entity.Name = name.Trim();
        entity.ServiceTypeId = serviceTypeId;
        entity.OwningBranchId = owningBranchId;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinition>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceDefinition>> ReactivateAsync(
        int serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(adminError);
        }

        var entity = await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceDefinition), serviceDefId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    "A soft-deleted ServiceDefinition cannot be reactivated."));
        }

        if (entity.IsActive)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyActive,
                    "ServiceDefinition is already active."));
        }

        // §9.7 scopes the audit-write requirement to deactivation and
        // soft delete. Reactivation writes no audit row.
        entity.IsActive = true;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinition>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceDefinition>> DeactivateAsync(
        int serviceDefId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(adminError);
        }

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

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(ServiceDefinition),
            EntityId = entity.ServiceDefId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinition>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceDefinition>> SoftDeleteAsync(
        int serviceDefId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(adminError);
        }

        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceDefinition), serviceDefId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Service definition is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(ServiceDefinition), serviceDefId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(ServiceDefinition),
            EntityId = entity.ServiceDefId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<ServiceDefinition>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinition>.Success(entity);
    }
}
