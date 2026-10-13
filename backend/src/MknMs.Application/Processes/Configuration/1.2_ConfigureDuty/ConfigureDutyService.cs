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

    Task<ConfigurationResult<Duty>> CreateAsync(string name, int actingAdminId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Duty>> RenameAsync(int dutyId, string newName, int actingAdminId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Duty>> DeactivateAsync(
        int dutyId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Duty>> SoftDeleteAsync(
        int dutyId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureDutyService : IConfigureDutyService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ConfigureDutyService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
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

    public async Task<ConfigurationResult<Duty>> CreateAsync(string name, int actingAdminId, CancellationToken cancellationToken = default)
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

    public async Task<ConfigurationResult<Duty>> RenameAsync(int dutyId, string newName, int actingAdminId, CancellationToken cancellationToken = default)
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

    public async Task<ConfigurationResult<Duty>> DeactivateAsync(
        int dutyId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
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

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Duty),
            EntityId = entity.DutyId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Duty>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Duty>.Success(entity);
    }

    public async Task<ConfigurationResult<Duty>> SoftDeleteAsync(
        int dutyId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.Duties
            .FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Duty), dutyId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Duty is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(Duty), dutyId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<Duty>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<Duty>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Duty),
            EntityId = entity.DutyId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Duty>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Duty>.Success(entity);
    }
}
