namespace MknMs.Application.Common;

/// <summary>
/// Prepares the reference fallbacks and dependent-row changes that
/// §9.7 requires when a configuration entity is soft-deleted.
/// </summary>
/// <remarks>
/// The cascade service prepares changes; it does not commit them.
/// The calling process coordinates the operation:
///
///   1. Apply the cascade rules through ApplyAsync.
///   2. If the cascade returns Blocked, return Failure without
///      touching the parent or writing an audit row.
///   3. Mark the parent as soft-deleted.
///   4. Add the audit row through ConfigurationLifecycleService,
///      passing the cascade's ConsequencesText.
///   5. Call SaveChangesAsync once.
///
/// The dependent changes, the parent's state change, and the audit
/// row therefore commit or roll back together (§9.8).
///
/// Each entity type has one rule, matching §9.7's table. Rules are
/// added per-entity in their own commits, matching the entity's
/// soft-delete method landing.
///
/// Specification: §9.7, §9.8.
/// </remarks>
public interface IConfigurationCascadeService
{
    Task<ConfigurationResult<CascadeOutcome>> ApplyAsync(
        string entityType,
        int entityId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a cascade application.
/// </summary>
public sealed class CascadeOutcome
{
    private CascadeOutcome(bool blocked, string? blockedReason, string consequencesText)
    {
        IsBlocked = blocked;
        BlockedReason = blockedReason;
        ConsequencesText = consequencesText;
    }

    public bool IsBlocked { get; }

    public string? BlockedReason { get; }

    public string ConsequencesText { get; }

    public static CascadeOutcome Blocked(string reason) =>
        new(true, reason, string.Empty);

    public static CascadeOutcome Applied(string consequencesText) =>
        new(false, null, consequencesText);
}

public sealed class ConfigurationCascadeService : IConfigurationCascadeService
{
    private readonly MknDbContext _db;

    public ConfigurationCascadeService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<ConfigurationResult<CascadeOutcome>> ApplyAsync(
        string entityType,
        int entityId,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(entityType))
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "EntityType is required."));
        }

        if (entityId <= 0)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "EntityId must be positive."));
        }

        switch (entityType)
        {
            case nameof(Role):
                return await ApplyRoleCascadeAsync(entityId, cancellationToken);

            case nameof(Duty):
                return await ApplyDutyCascadeAsync(entityId, cancellationToken);

            default:
                return ConfigurationResult<CascadeOutcome>.Success(
                    CascadeOutcome.Applied("No dependent changes apply."));
        }
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyRoleCascadeAsync(
        int roleId,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
        if (role is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"Role {roleId} does not exist."));
        }

        if (role.IsDefault)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    "The default Role cannot be soft-deleted."));
        }

        var defaultRole = await _db.Roles
            .FirstOrDefaultAsync(r => r.IsDefault && !r.IsDeleted, cancellationToken);
        if (defaultRole is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ConfigurationMissing,
                    "No default Role is configured; the fallback target cannot be resolved."));
        }

        var affectedMembers = await _db.Members
            .Where(m => m.RoleId == roleId)
            .ToListAsync(cancellationToken);
        foreach (var member in affectedMembers)
        {
            member.RoleId = defaultRole.RoleId;
        }

        var affectedRules = await _db.DutyRules
            .Where(r => r.CriteriaType == "Role"
                && r.CriteriaValue == role.Name
                && !r.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var rule in affectedRules)
        {
            rule.IsDeleted = true;
            rule.IsActive = false;
        }

        var text =
            $"{affectedMembers.Count} member(s) fell back to the default Role; "
            + $"{affectedRules.Count} Duty Rule(s) were soft-deleted.";

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyDutyCascadeAsync(
        int dutyId,
        CancellationToken cancellationToken)
    {
        var duty = await _db.Duties
            .FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (duty is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"Duty {dutyId} does not exist."));
        }

        var sddCount = await _db.ServiceDefinitionDuties
            .CountAsync(d => d.DutyId == dutyId, cancellationToken);
        var edCount = await _db.EventDuties
            .CountAsync(d => d.DutyId == dutyId, cancellationToken);

        if (sddCount > 0 || edCount > 0)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    $"Duty {dutyId} is referenced by "
                    + $"{sddCount} Service Definition Duty row(s) and "
                    + $"{edCount} Event Duty row(s)."));
        }

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied("No dependent changes apply."));
    }
}
