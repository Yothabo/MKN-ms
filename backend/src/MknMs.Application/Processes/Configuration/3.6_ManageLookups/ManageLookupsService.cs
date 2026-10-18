
namespace MknMs.Application.Processes.Configuration.ManageLookups;

/// <summary>
/// Subprocess 3.6 — Manage D10 Lookups.
/// </summary>
/// <remarks>
/// Soft-delete operations for the five D10 lookup entities. §3.0
/// does not name lookup CRUD; the E4m scope is the block-only
/// cascade §9.7 gives each lookup.
///
/// Each lookup's soft delete follows the same body shape:
/// validate, load, refuse if already soft-deleted, require
/// acknowledgement, apply the cascade (block-only), set
/// IsDeleted = true and IsActive = false, write the audit row, one
/// SaveChangesAsync.
///
/// The reference checks are per lookup and live in
/// ConfigurationCascadeService. They are not generalised: an
/// OutcomeState's setting references are its four keys, an
/// AssignmentStatus's are three different keys, and so on.
///
/// Specification: §4 (lookups), §9.7, §15.8.15.
/// </remarks>
public interface IManageLookupsService
{
    Task<ConfigurationResult<OutcomeState>> SoftDeleteOutcomeStateAsync(
        int outcomeStateId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AssignmentStatus>> SoftDeleteAssignmentStatusAsync(
        int assignmentStatusId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<TimeOfDay>> SoftDeleteTimeOfDayAsync(
        int timeOfDayId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceType>> SoftDeleteServiceTypeAsync(
        int serviceTypeId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<PermissionTier>> SoftDeletePermissionTierAsync(
        int permissionTierId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageLookupsService : IManageLookupsService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageLookupsService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public Task<ConfigurationResult<OutcomeState>> SoftDeleteOutcomeStateAsync(
        int outcomeStateId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default) =>
        SoftDeleteAsync<OutcomeState>(
            outcomeStateId, actingAdminId, approvedByAdminId,
            consequencesAcknowledged, reason,
            id => _db.OutcomeStates.FirstOrDefaultAsync(o => o.OutcomeStateId == id, cancellationToken),
            o => o.OutcomeStateId, o => o.Name,
            o => o.IsDeleted, (o, v) => o.IsDeleted = v,
            o => o.IsActive, (o, v) => o.IsActive = v,
            cancellationToken);

    public Task<ConfigurationResult<AssignmentStatus>> SoftDeleteAssignmentStatusAsync(
        int assignmentStatusId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default) =>
        SoftDeleteAsync<AssignmentStatus>(
            assignmentStatusId, actingAdminId, approvedByAdminId,
            consequencesAcknowledged, reason,
            id => _db.AssignmentStatuses.FirstOrDefaultAsync(s => s.AssignmentStatusId == id, cancellationToken),
            s => s.AssignmentStatusId, s => s.Name,
            s => s.IsDeleted, (s, v) => s.IsDeleted = v,
            s => s.IsActive, (s, v) => s.IsActive = v,
            cancellationToken);

    public Task<ConfigurationResult<TimeOfDay>> SoftDeleteTimeOfDayAsync(
        int timeOfDayId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default) =>
        SoftDeleteAsync<TimeOfDay>(
            timeOfDayId, actingAdminId, approvedByAdminId,
            consequencesAcknowledged, reason,
            id => _db.TimeOfDays.FirstOrDefaultAsync(t => t.TimeOfDayId == id, cancellationToken),
            t => t.TimeOfDayId, t => t.Name,
            t => t.IsDeleted, (t, v) => t.IsDeleted = v,
            t => t.IsActive, (t, v) => t.IsActive = v,
            cancellationToken);

    public Task<ConfigurationResult<ServiceType>> SoftDeleteServiceTypeAsync(
        int serviceTypeId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default) =>
        SoftDeleteAsync<ServiceType>(
            serviceTypeId, actingAdminId, approvedByAdminId,
            consequencesAcknowledged, reason,
            id => _db.ServiceTypes.FirstOrDefaultAsync(s => s.ServiceTypeId == id, cancellationToken),
            s => s.ServiceTypeId, s => s.Name,
            s => s.IsDeleted, (s, v) => s.IsDeleted = v,
            s => s.IsActive, (s, v) => s.IsActive = v,
            cancellationToken);

    public Task<ConfigurationResult<PermissionTier>> SoftDeletePermissionTierAsync(
        int permissionTierId, int actingAdminId, int approvedByAdminId,
        bool consequencesAcknowledged, string reason,
        CancellationToken cancellationToken = default) =>
        SoftDeleteAsync<PermissionTier>(
            permissionTierId, actingAdminId, approvedByAdminId,
            consequencesAcknowledged, reason,
            id => _db.PermissionTiers.FirstOrDefaultAsync(t => t.PermissionTierId == id, cancellationToken),
            t => t.PermissionTierId, t => t.Name,
            t => t.IsDeleted, (t, v) => t.IsDeleted = v,
            t => t.IsActive, (t, v) => t.IsActive = v,
            cancellationToken);

    // -----------------------------------------------------------------
    // Shared body. Each lookup has the same shape: an id, a name,
    // and the two flags. The type-specific parts are supplied as
    // delegates so the body is not duplicated five times.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<T>> SoftDeleteAsync<T>(
        int id,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        Func<int, Task<T?>> loadAsync,
        Func<T, int> getId,
        Func<T, string> getName,
        Func<T, bool> getIsDeleted,
        Action<T, bool> setIsDeleted,
        Func<T, bool> getIsActive,
        Action<T, bool> setIsActive,
        CancellationToken cancellationToken)
        where T : class
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<T>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await loadAsync(id);
        if (entity is null)
        {
            throw new EntityNotFoundException(typeof(T).Name, id);
        }

        if (getIsDeleted(entity))
        {
            return ConfigurationResult<T>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    $"{typeof(T).Name} is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<T>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            typeof(T).Name, id, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<T>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<T>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        setIsDeleted(entity, true);
        setIsActive(entity, false);

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = typeof(T).Name,
            EntityId = getId(entity),
            EntityName = getName(entity),
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<T>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<T>.Success(entity);
    }
}
