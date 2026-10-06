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

    Task<ConfigurationResult<Role>> CreateAsync(string name, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Role>> RenameAsync(int roleId, string newName, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Role>> DeactivateAsync(int roleId, CancellationToken cancellationToken = default);
}

public sealed class ConfigureRoleService : IConfigureRoleService
{
    private readonly MknDbContext _db;

    public ConfigureRoleService(MknDbContext db)
    {
        _db = db;
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

    public async Task<ConfigurationResult<Role>> CreateAsync(string name, CancellationToken cancellationToken = default)
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

    public async Task<ConfigurationResult<Role>> RenameAsync(int roleId, string newName, CancellationToken cancellationToken = default)
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

    public async Task<ConfigurationResult<Role>> DeactivateAsync(int roleId, CancellationToken cancellationToken = default)
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
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Role>.Success(entity);
    }
}
