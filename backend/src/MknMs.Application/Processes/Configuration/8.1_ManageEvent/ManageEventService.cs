
namespace MknMs.Application.Processes.Configuration.ManageEvent;

/// <summary>
/// Subprocess of 8.0 — Manage Event.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists Event rows, and manages the
/// EventBranch set for each Event. Type is free text. May span
/// multiple days — StartDate and EndDate are both fields.
///
/// HostBranchId is the branch at which the event is held. The Event
/// carries no Location field; the host branch expresses where the
/// event is held. AttendingBranchIds is the set of branches
/// attending; it is independent of the host and may be empty. The
/// host need not appear in the attendee set (§4, B20).
///
/// UsesAttendanceRegister, when set, overrides the global attendance
/// register state for the event's own occurrences. Null means no
/// override.
///
/// Specification: §4 (Event, Event Branch), §9.1.11, §15.8.8,
/// §15.8.9.
/// </remarks>
public interface IManageEventService
{
    Task<IReadOnlyList<Event>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Event?> GetAsync(int eventId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EventBranch>> ListBranchesAsync(
        int eventId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Event>> CreateAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string type,
        int hostBranchId,
        IReadOnlyList<int>? attendingBranchIds,
        bool? usesAttendanceRegister,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Event>> UpdateAsync(
        int eventId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string type,
        int hostBranchId,
        IReadOnlyList<int>? attendingBranchIds,
        bool? usesAttendanceRegister,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Event>> DeactivateAsync(
        int eventId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Event>> SoftDeleteAsync(
        int eventId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageEventService : IManageEventService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageEventService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<IReadOnlyList<Event>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Events.Where(e => !e.IsDeleted);
        if (!includeInactive)
        {
            query = query.Where(e => e.IsActive);
        }
        return await query.OrderBy(e => e.EventId).ToListAsync(cancellationToken);
    }

    public async Task<Event?> GetAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
    }

    public async Task<IReadOnlyList<EventBranch>> ListBranchesAsync(
        int eventId,
        CancellationToken cancellationToken = default)
    {
        return await _db.EventBranches
            .Where(b => b.EventId == eventId)
            .OrderBy(b => b.BranchId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<Event>> CreateAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string type,
        int hostBranchId,
        IReadOnlyList<int>? attendingBranchIds,
        bool? usesAttendanceRegister,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            name, startDate, endDate, type, hostBranchId,
            attendingBranchIds, cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<Event>.Failure(validation);
        }

        var entity = new Event
        {
            Name = name.Trim(),
            StartDate = startDate,
            EndDate = endDate,
            Type = type.Trim(),
            HostBranchId = hostBranchId,
            UsesAttendanceRegister = usesAttendanceRegister,
            IsActive = true,
            IsDeleted = false,
        };
        _db.Events.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await ReplaceAttendingBranchesAsync(
            entity.EventId, attendingBranchIds, cancellationToken);

        return ConfigurationResult<Event>.Success(entity);
    }

    public async Task<ConfigurationResult<Event>> UpdateAsync(
        int eventId,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string type,
        int hostBranchId,
        IReadOnlyList<int>? attendingBranchIds,
        bool? usesAttendanceRegister,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            name, startDate, endDate, type, hostBranchId,
            attendingBranchIds, cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<Event>.Failure(validation);
        }

        var entity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Event), eventId);
        }

        entity.Name = name.Trim();
        entity.StartDate = startDate;
        entity.EndDate = endDate;
        entity.Type = type.Trim();
        entity.HostBranchId = hostBranchId;
        entity.UsesAttendanceRegister = usesAttendanceRegister;
        await _db.SaveChangesAsync(cancellationToken);

        await ReplaceAttendingBranchesAsync(
            eventId, attendingBranchIds, cancellationToken);

        return ConfigurationResult<Event>.Success(entity);
    }

    public async Task<ConfigurationResult<Event>> DeactivateAsync(
        int eventId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Event), eventId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<Event>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Event is already inactive."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Event>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Event),
            EntityId = entity.EventId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Event>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Event>.Success(entity);
    }

    public async Task<ConfigurationResult<Event>> SoftDeleteAsync(
        int eventId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<Event>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.Events
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Event), eventId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<Event>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Event is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Event>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(Event), eventId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<Event>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<Event>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Event),
            EntityId = entity.EventId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Event>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Event>.Success(entity);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private async Task ReplaceAttendingBranchesAsync(
        int eventId,
        IReadOnlyList<int>? attendingBranchIds,
        CancellationToken cancellationToken)
    {
        var existing = await _db.EventBranches
            .Where(b => b.EventId == eventId)
            .ToListAsync(cancellationToken);
        _db.EventBranches.RemoveRange(existing);

        if (attendingBranchIds is not null)
        {
            foreach (var branchId in attendingBranchIds.Distinct())
            {
                _db.EventBranches.Add(new EventBranch
                {
                    EventId = eventId,
                    BranchId = branchId,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ConfigurationError?> ValidateAsync(
        string name,
        DateOnly startDate,
        DateOnly endDate,
        string type,
        int hostBranchId,
        IReadOnlyList<int>? attendingBranchIds,
        CancellationToken cancellationToken)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required.");
        }
        if (!ConfigurationValidation.IsValidName(type))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Type is required.");
        }
        if (endDate < startDate)
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                "EndDate cannot be before StartDate.");
        }

        var host = await _db.Branches
            .FirstOrDefaultAsync(b => b.BranchId == hostBranchId, cancellationToken);
        if (host is null)
        {
            return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                "Host branch does not exist.");
        }

        if (attendingBranchIds is not null && attendingBranchIds.Count > 0)
        {
            var ids = attendingBranchIds.Distinct().ToList();
            var found = await _db.Branches
                .Where(b => ids.Contains(b.BranchId))
                .Select(b => b.BranchId)
                .ToListAsync(cancellationToken);

            var missing = ids.Except(found).ToList();
            if (missing.Count > 0)
            {
                return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    $"Attending branches do not exist: {string.Join(", ", missing)}.");
            }
        }

        return null;
    }
}
