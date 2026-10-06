namespace MknMs.Application.Processes.Configuration.ManageEventDuty;

/// <summary>
/// Subprocess of 8.0 — Manage Event Duty.
/// </summary>
/// <remarks>
/// Creates, edits, and removes EventDuty rows. An EventDuty is an
/// ad-hoc role on a specific Program Item, with a free-text Label and
/// a direct member assignment — no Eligibility or Priority run. It is
/// not a reference to the Duty table.
///
/// AssignmentStatusId is optional and references the same lookup used
/// by Roster Assignment.
///
/// Specification: §4 (Event Duty), §9.1.11.
/// </remarks>
public interface IManageEventDutyService
{
    Task<IReadOnlyList<EventDuty>> ListForItemAsync(
        int programItemId,
        CancellationToken cancellationToken = default);

    Task<EventDuty?> GetAsync(int eventDutyId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<EventDuty>> CreateAsync(
        int programItemId,
        string label,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<EventDuty>> UpdateAsync(
        int eventDutyId,
        string label,
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
        string label,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            label, assignedMemberId, assignmentStatusId, cancellationToken);
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
            Label = label.Trim(),
            AssignedMemberId = assignedMemberId,
            AssignmentStatusId = assignmentStatusId,
        };
        _db.EventDuties.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<EventDuty>.Success(entity);
    }

    public async Task<ConfigurationResult<EventDuty>> UpdateAsync(
        int eventDutyId,
        string label,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateAsync(
            label, assignedMemberId, assignmentStatusId, cancellationToken);
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

        entity.Label = label.Trim();
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

        // EventDuty is a small ad-hoc record with no downstream
        // references. Removing it is the correct behavior when the
        // administrator decides the role is no longer needed. This is
        // the one deletion permitted in the configuration layer, and
        // it is permitted only because nothing references an
        // EventDuty row.
        _db.EventDuties.Remove(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<bool>.Success(true);
    }

    private async Task<ConfigurationError?> ValidateAsync(
        string label,
        int assignedMemberId,
        int? assignmentStatusId,
        CancellationToken cancellationToken)
    {
        if (!ConfigurationValidation.IsValidName(label))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Label is required.");
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
