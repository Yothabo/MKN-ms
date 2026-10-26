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

    Task<ConfigurationResult<ProgramItem>> SoftDeleteAsync(
        int itemId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageProgramItemService : IManageProgramItemService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageProgramItemService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<IReadOnlyList<ProgramItem>> ListForProgramAsync(
        int programId,
        CancellationToken cancellationToken = default)
    {
        return await _db.ProgramItems
            .Where(i => i.ProgramId == programId && !i.IsDeleted)
            .OrderBy(i => i.SequenceOrder)
            .ThenBy(i => i.ItemId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ProgramItem?> GetAsync(int itemId, CancellationToken cancellationToken = default)
    {
        return await _db.ProgramItems.FirstOrDefaultAsync(i => i.ItemId == itemId && !i.IsDeleted, cancellationToken);
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

        // §C7: DateTimeOffset columns are instants, persisted in UTC.
        // Npgsql rejects non-UTC offsets on 'timestamp with time zone'.
        // Normalise the caller's offset here so every caller — HTTP,
        // bot, tests — writes the same UTC instant.
        var startUtc = scheduledStart.ToUniversalTime();
        var endUtc = scheduledEnd.ToUniversalTime();

        var entity = new ProgramItem
        {
            ProgramId = programId,
            SequenceOrder = sequenceOrder,
            Title = title.Trim(),
            ScheduledStart = startUtc,
            ScheduledEnd = endUtc,
            ServiceDefId = serviceDefId,
        };
        _db.ProgramItems.Add(entity);

        if (serviceDefId is int defId)
        {
            await CreateEventSourcedOccurrenceAsync(
                entity, scheduledStart, program.EventId, defId, actingAdminId, cancellationToken);
        }

        // §9.8: the ProgramItem and the event-sourced occurrence are one
        // atomic unit. Both are added to the context and committed by a
        // single SaveChangesAsync below.
        await _db.SaveChangesAsync(cancellationToken);

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

        // §C7: DateTimeOffset columns are instants, persisted in UTC.
        entity.SequenceOrder = sequenceOrder;
        entity.Title = title.Trim();
        entity.ScheduledStart = scheduledStart.ToUniversalTime();
        entity.ScheduledEnd = scheduledEnd.ToUniversalTime();
        entity.ServiceDefId = serviceDefId;

        // If the ServiceDefId transitioned from null to a value, create
        // the event-sourced occurrence. If it changed from one value to
        // another, the specification does not require deleting the old
        // occurrence or creating a new one — cleanup is an administrator
        // action, not a system behavior (§9.1.11).
        if (previousServiceDefId is null && serviceDefId is int defId)
        {
            var program = await _db.Programs
                .FirstAsync(p => p.ProgramId == entity.ProgramId, cancellationToken);
            await CreateEventSourcedOccurrenceAsync(
                entity, scheduledStart, program.EventId, defId, actingAdminId, cancellationToken);
        }

        // §9.8: the ProgramItem field changes and the occurrence (when
        // created) are one atomic unit. Both are tracked and committed
        // by a single SaveChangesAsync below.
        await _db.SaveChangesAsync(cancellationToken);

        return ConfigurationResult<ProgramItem>.Success(entity);
    }

    public async Task<ConfigurationResult<ProgramItem>> SoftDeleteAsync(
        int itemId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<ProgramItem>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.ProgramItems
            .FirstOrDefaultAsync(i => i.ItemId == itemId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ProgramItem), itemId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<ProgramItem>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Program Item is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<ProgramItem>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(ProgramItem), itemId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<ProgramItem>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<ProgramItem>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(ProgramItem),
            EntityId = entity.ItemId,
            EntityName = entity.Title,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<ProgramItem>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
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

        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return adminError;
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
        DateTimeOffset wallClockStart,
        int eventId,
        int serviceDefId,
        int actingAdminId,
        CancellationToken cancellationToken)
    {
        var definition = await _db.ServiceDefinitions
            .FirstAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);

        // §C7: StartTime is a wall-clock time. It is the caller's
        // local time as written in the ProgramItem request, NOT the
        // UTC wall-clock derived from the normalised instant. Date is
        // the calendar date that the caller's wall-clock falls on.
        // The stored ProgramItem.ScheduledStart remains a UTC instant
        // (D1, commit d105622); only the derived ServiceOccurrence
        // fields use the caller's wall-clock.
        var occurrence = new ServiceOccurrence
        {
            ScheduleId = null,
            EventId = eventId,
            Date = DateOnly.FromDateTime(wallClockStart.Date),
            ServiceTypeId = definition.ServiceTypeId,
            StartTime = TimeOnly.FromDateTime(wallClockStart.DateTime),
            FillStatusId = null,
            GeneratedBy = "Administrator",
            CreatedBy = actingAdminId,
            ChangedBy = null,
        };

        // The caller commits. §9.8: the ProgramItem mutation and this
        // occurrence are one atomic unit; neither is committed before
        // the other.
        _db.ServiceOccurrences.Add(occurrence);
    }
}
