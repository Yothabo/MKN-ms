namespace MknMs.Application.Processes.Configuration.ConfigureRole;

/// <summary>
/// Subprocess 1.1 — Configure Role.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists Role rows. A Role is a name.
/// Nothing more. It carries no reference to Duty or to anything else.
///
/// Specification: §4 (Role), §9.1.2, §9.1.1.
/// </remarks>
public interface IConfigureRoleService
{
    Task<IReadOnlyList<Role>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default);

    Task<Role?> GetAsync(int roleId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Role>> CreateAsync(string name, int actingAdminId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Role>> RenameAsync(int roleId, string newName, int actingAdminId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Role>> DeactivateAsync(
        int roleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Role>> SoftDeleteAsync(
        int roleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureRoleService : IConfigureRoleService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ConfigureRoleService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<IReadOnlyList<Role>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Roles.Where(r => !r.IsDeleted);
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        return await query.OrderBy(r => r.RoleId).ToListAsync(cancellationToken);
    }

    public async Task<Role?> GetAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId && !r.IsDeleted, cancellationToken);
    }

    public async Task<ConfigurationResult<Role>> CreateAsync(string name, int actingAdminId, CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<Role>.Failure(adminError);
        }

        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = new Role { Name = name.Trim(), IsActive = true };
        _db.Roles.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Role>.Success(entity);
    }

    public async Task<ConfigurationResult<Role>> RenameAsync(int roleId, string newName, int actingAdminId, CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<Role>.Failure(adminError);
        }

        if (!ConfigurationValidation.IsValidName(newName))
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var entity = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Role), roleId);
        }

        entity.Name = newName.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Role>.Success(entity);
    }

    public async Task<ConfigurationResult<Role>> DeactivateAsync(
        int roleId,
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
            return ConfigurationResult<Role>.Failure(adminError);
        }

        var entity = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Role), roleId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Role is already inactive."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Role),
            EntityId = entity.RoleId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Role>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Role>.Success(entity);
    }

    public async Task<ConfigurationResult<Role>> SoftDeleteAsync(
        int roleId,
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
            return ConfigurationResult<Role>.Failure(adminError);
        }

        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Role), roleId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Role is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        // Apply the cascade rules before touching the parent. If the
        // cascade blocks, nothing is changed.
        var cascade = await _cascade.ApplyAsync(
            nameof(Role), roleId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<Role>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<Role>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Role),
            EntityId = entity.RoleId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Role>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Role>.Success(entity);
    }
}
