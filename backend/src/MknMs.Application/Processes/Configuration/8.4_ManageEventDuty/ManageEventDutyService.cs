
namespace MknMs.Application.Processes.Configuration.ManageEventDuty;

/// <summary>
/// Subprocess of 8.0 — Manage Event Duty.
/// </summary>
/// <remarks>
/// Creates, edits, and removes EventDuty rows. An EventDuty is a
/// requirement on a specific Program Item for a real Duty, with a
/// required slot count and an optional Service Definition scope.
///
/// DutyId references the Duty table. When ServiceDefId is set, the
/// duty uses that service's per-service rules on this event; when
/// null, the duty's global rules apply. AssignmentStatusId is
/// optional and references the same lookup used by Roster
/// Assignment.
///
/// Specification: §4 (Event Duty), §9.1.11, §15.8.10.
/// </remarks>
public interface IManageEventDutyService
{
    Task<IReadOnlyList<EventDuty>> ListForItemAsync(
        int programItemId,
        CancellationToken cancellationToken = default);

    Task<EventDuty?> GetAsync(int eventDutyId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<EventDuty>> CreateAsync(
        int programItemId,
        int dutyId,
        int? serviceDefId,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<EventDuty>> UpdateAsync(
        int eventDutyId,
        int dutyId,
        int? serviceDefId,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<bool>> RemoveAsync(
        int eventDutyId,
        CancellationToken cancellationToken = default);
}

public sealed class ManageEventDutyService : IManageEventDutyService
{
    private readonly MknDbContext _db;

    public ManageEventDutyService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<EventDuty>> ListForItemAsync(
        int programItemId,
        CancellationToken cancellationToken = default)
    {
        return await _db.EventDuties
            .Where(d => d.ProgramItemId == programItemId)
            .OrderBy(d => d.EventDutyId)
            .ToListAsync(cancellationToken);
    }

    public async Task<EventDuty?> GetAsync(int eventDutyId, CancellationToken cancellationToken = default)
    {
        return await _db.EventDuties
            .FirstOrDefaultAsync(d => d.EventDutyId == eventDutyId, cancellationToken);
    }

    public async Task<ConfigurationResult<EventDuty>> CreateAsync(
        int programItemId,
        int dutyId,
        int? serviceDefId,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            dutyId, serviceDefId, assignedMemberId, assignmentStatusId,
            cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<EventDuty>.Failure(validation);
        }

        var item = await _db.ProgramItems
            .FirstOrDefaultAsync(i => i.ItemId == programItemId, cancellationToken);
        if (item is null)
        {
            return ConfigurationResult<EventDuty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Program item does not exist."));
        }

        var entity = new EventDuty
        {
            ProgramItemId = programItemId,
            DutyId = dutyId,
            ServiceDefId = serviceDefId,
            AssignedMemberId = assignedMemberId,
            AssignmentStatusId = assignmentStatusId,
            IsActive = true,
            IsDeleted = false,
        };
        _db.EventDuties.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<EventDuty>.Success(entity);
    }

    public async Task<ConfigurationResult<EventDuty>> UpdateAsync(
        int eventDutyId,
        int dutyId,
        int? serviceDefId,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            dutyId, serviceDefId, assignedMemberId, assignmentStatusId,
            cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<EventDuty>.Failure(validation);
        }

        var entity = await _db.EventDuties
            .FirstOrDefaultAsync(d => d.EventDutyId == eventDutyId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(EventDuty), eventDutyId);
        }

        entity.DutyId = dutyId;
        entity.ServiceDefId = serviceDefId;
        entity.AssignedMemberId = assignedMemberId;
        entity.AssignmentStatusId = assignmentStatusId;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<EventDuty>.Success(entity);
    }

    public async Task<ConfigurationResult<bool>> RemoveAsync(
        int eventDutyId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.EventDuties
            .FirstOrDefaultAsync(d => d.EventDutyId == eventDutyId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(EventDuty), eventDutyId);
        }

        _db.EventDuties.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<bool>.Success(true);
    }

    private async Task<ConfigurationError?> ValidateAsync(
        int dutyId,
        int? serviceDefId,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken)
    {
        var duty = await _db.Duties
            .FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (duty is null)
        {
            return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                "Duty does not exist.");
        }

        if (serviceDefId is int sdId)
        {
            var def = await _db.ServiceDefinitions
                .FirstOrDefaultAsync(s => s.ServiceDefId == sdId, cancellationToken);
            if (def is null)
            {
                return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Service definition does not exist.");
            }
        }

        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.MemberId == assignedMemberId, cancellationToken);
        if (member is null)
        {
            return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                "Assigned member does not exist.");
        }

        if (assignmentStatusId is int statusId)
        {
            var status = await _db.AssignmentStatuses
                .FirstOrDefaultAsync(s => s.AssignmentStatusId == statusId, cancellationToken);
            if (status is null)
            {
                return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Assignment status does not exist.");
            }
        }

        return null;
    }
}
