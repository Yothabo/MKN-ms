namespace MknMs.Application.Processes.Configuration.ManageProgramItem;

/// <summary>
/// Subprocess of 8.0 — Manage Program Item.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists ProgramItem rows within a
/// Program. Status is not stored — it is computed at read time from
/// ScheduledStart, ScheduledEnd, and the current time.
///
/// When ServiceDefId is set — at creation or by later edit — the
/// process creates an event-sourced ServiceOccurrence directly. This
/// is the sole write from the configuration layer to an operational
/// store, and it is exercised only when the administrator explicitly
/// links a Program Item to a Service Definition.
///
/// The event-sourced occurrence carries:
///   - Date       ← the date portion of ProgramItem.ScheduledStart
///   - StartTime  ← the time portion of ProgramItem.ScheduledStart
///   - ServiceTypeId ← inherited from the linked ServiceDefinition
///   - EventId    ← the owning Event
///   - ScheduleId ← null
///   - GeneratedBy = "Administrator"
///   - CreatedBy  = the acting administrator's id
///   - FillStatusId = null
///
/// Specification: §4 (Program Item), §7 (Event-sourced occurrences), §9.1.11.
/// </remarks>
public interface IManageProgramItemService
{
    Task<IReadOnlyList<ProgramItem>> ListForProgramAsync(
        int programId,
        CancellationToken cancellationToken = default);

    Task<ProgramItem?> GetAsync(int itemId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ProgramItem>> CreateAsync(
        int programId,
        int sequenceOrder,
        string title,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        int? serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ProgramItem>> UpdateAsync(
        int itemId,
        int sequenceOrder,
        string title,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        int? serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ProgramItem>> DeactivateAsync(
        int itemId,
        CancellationToken cancellationToken = default);
}

public sealed class ManageProgramItemService : IManageProgramItemService
{
    private readonly MknDbContext _db;

    public ManageProgramItemService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<ProgramItem>> ListForProgramAsync(
        int programId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ProgramItems
            .Where(i => i.ProgramId == programId)
            .OrderBy(i => i.SequenceOrder)
            .ThenBy(i => i.ItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProgramItem?> GetAsync(int itemId, CancellationToken cancellationToken = default)
    {
        return await _db.ProgramItems.FirstOrDefaultAsync(i => i.ItemId == itemId, cancellationToken);
    }

    public async Task<ConfigurationResult<ProgramItem>> CreateAsync(
        int programId,
        int sequenceOrder,
        string title,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        int? serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInputAsync(
            sequenceOrder, title, scheduledStart, scheduledEnd,
            serviceDefId, actingAdminId, cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<ProgramItem>.Failure(validation);
        }

        var program = await _db.Programs
            .FirstOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);
        if (program is null)
        {
            return ConfigurationResult<ProgramItem>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Program does not exist."));
        }

        var entity = new ProgramItem
        {
            ProgramId = programId,
            SequenceOrder = sequenceOrder,
            Title = title.Trim(),
            ScheduledStart = scheduledStart,
            ScheduledEnd = scheduledEnd,
            ServiceDefId = serviceDefId,
        };
        _db.ProgramItems.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        if (serviceDefId is int defId)
        {
            await CreateEventSourcedOccurrenceAsync(entity, program.EventId, defId, actingAdminId, cancellationToken);
        }

        return ConfigurationResult<ProgramItem>.Success(entity);
    }

    public async Task<ConfigurationResult<ProgramItem>> UpdateAsync(
        int itemId,
        int sequenceOrder,
        string title,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        int? serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateInputAsync(
            sequenceOrder, title, scheduledStart, scheduledEnd,
            serviceDefId, actingAdminId, cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<ProgramItem>.Failure(validation);
        }

        var entity = await _db.ProgramItems
            .FirstOrDefaultAsync(i => i.ItemId == itemId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ProgramItem), itemId);
        }

        var previousServiceDefId = entity.ServiceDefId;

        entity.SequenceOrder = sequenceOrder;
        entity.Title = title.Trim();
        entity.ScheduledStart = scheduledStart;
        entity.ScheduledEnd = scheduledEnd;
        entity.ServiceDefId = serviceDefId;

        await _db.SaveChangesAsync(cancellationToken);

        // If the ServiceDefId transitioned from null to a value, create
        // the event-sourced occurrence. If it changed from one value to
        // another, the specification does not require deleting the old
        // occurrence or creating a new one — cleanup is an administrator
        // action, not a system behavior (§9.1.11).
        if (previousServiceDefId is null && serviceDefId is int defId)
        {
            var program = await _db.Programs
                .FirstAsync(p => p.ProgramId == entity.ProgramId, cancellationToken);
            await CreateEventSourcedOccurrenceAsync(entity, program.EventId, defId, actingAdminId, cancellationToken);
        }

        return ConfigurationResult<ProgramItem>.Success(entity);
    }

    public async Task<ConfigurationResult<ProgramItem>> DeactivateAsync(
        int itemId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ProgramItems
            .FirstOrDefaultAsync(i => i.ItemId == itemId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ProgramItem), itemId);
        }

        // ProgramItem has no stored status field. Its live state is
        // computed from scheduled times. There is nothing to
        // "deactivate" in the schema sense; this method is a no-op
        // placeholder for symmetry. In practice, removing a ProgramItem
        // is not a system-supported operation — the row remains, and
        // status computation reflects whether the item is in the past,
        // current, or future.
        return ConfigurationResult<ProgramItem>.Success(entity);
    }

    private async Task<ConfigurationError?> ValidateInputAsync(
        int sequenceOrder,
        string title,
        DateTimeOffset scheduledStart,
        DateTimeOffset scheduledEnd,
        int? serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken)
    {
        if (!ConfigurationValidation.IsNonNegative(sequenceOrder))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                "SequenceOrder must be a non-negative integer.");
        }
        if (!ConfigurationValidation.IsValidName(title))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Title is required.");
        }
        if (scheduledEnd < scheduledStart)
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                "ScheduledEnd cannot be before ScheduledStart.");
        }

        var admin = await _db.Admins
            .FirstOrDefaultAsync(a => a.AdminId == actingAdminId, cancellationToken);
        if (admin is null)
        {
            return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                "Acting administrator does not exist.");
        }

        if (serviceDefId is int defId)
        {
            var def = await _db.ServiceDefinitions
                .FirstOrDefaultAsync(s => s.ServiceDefId == defId, cancellationToken);
            if (def is null)
            {
                return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Service definition does not exist.");
            }
        }

        return null;
    }

    private async Task CreateEventSourcedOccurrenceAsync(
        ProgramItem item,
        int eventId,
        int serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken)
    {
        var definition = await _db.ServiceDefinitions
            .FirstAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);

        var scheduledStart = item.ScheduledStart;

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = null,
            EventId = eventId,
            Date = DateOnly.FromDateTime(scheduledStart.Date),
            ServiceTypeId = definition.ServiceTypeId,
            StartTime = TimeOnly.FromDateTime(scheduledStart.DateTime),
            FillStatusId = null,
            GeneratedBy = "Administrator",
            CreatedBy = actingAdminId,
            ChangedBy = null,
        };

        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync(cancellationToken);
    }
}
