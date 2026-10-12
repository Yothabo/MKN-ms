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

    Task<ConfigurationResult<Role>> DeactivateAsync(int roleId, int actingAdminId, CancellationToken cancellationToken = default);
}

public sealed class ConfigureRoleService : IConfigureRoleService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;

    public ConfigureRoleService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle)
    {
        _db = db;
        _lifecycle = lifecycle;
    }

    public async Task<IReadOnlyList<Role>> ListAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _db.Roles.AsQueryable();
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        return await query.OrderBy(r => r.RoleId).ToListAsync(cancellationToken);
    }

    public async Task<Role?> GetAsync(int roleId, CancellationToken cancellationToken = default)
    {
        return await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
    }

    public async Task<ConfigurationResult<Role>> CreateAsync(string name, int actingAdminId, CancellationToken cancellationToken = default)
    {
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

    public async Task<ConfigurationResult<Role>> DeactivateAsync(int roleId, int actingAdminId, CancellationToken cancellationToken = default)
    {
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

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Role),
            EntityId = entity.RoleId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.Deactivate,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = actingAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Role>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Role>.Success(entity);
    }
}
