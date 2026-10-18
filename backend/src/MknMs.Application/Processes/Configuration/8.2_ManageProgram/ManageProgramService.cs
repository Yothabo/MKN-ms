namespace MknMs.Application.Processes.Configuration.ManageProgram;

/// <summary>
/// Subprocess of 8.0 — Manage Program.
/// </summary>
/// <remarks>
/// Creates, edits, and lists Program rows. Exactly one Program per
/// Event — enforced by the unique index uq_program_event on
/// (event_id). A Program has no IsActive flag; it is deactivated only
/// by deactivating its Event.
///
/// Specification: §4 (Program), §9.1.11.
/// </remarks>
public interface IManageProgramService
{
    Task<Program?> GetForEventAsync(int eventId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Program>> CreateAsync(
        int eventId,
        string title,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Program>> UpdateAsync(
        int programId,
        string title,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Soft-deletes a Program. §9.7's Program row applies: the
    /// Program's ProgramItem children (and their EventDuty
    /// dependents) are physically removed; the Program row itself is
    /// retained with IsDeleted = true.
    ///
    /// No endpoint exposes this at the current phase — §9.7 states
    /// that a Program is deactivated only by deactivating its Event.
    /// The method exists for the cascade contract and for tests.
    /// </summary>
    Task<ConfigurationResult<Program>> SoftDeleteAsync(
        int programId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageProgramService : IManageProgramService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageProgramService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<Program?> GetForEventAsync(int eventId, CancellationToken cancellationToken = default)
    {
        return await _db.Programs.FirstOrDefaultAsync(p => p.EventId == eventId && !p.IsDeleted, cancellationToken);
    }

    public async Task<ConfigurationResult<Program>> CreateAsync(
        int eventId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(title))
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Title is required."));
        }

        var eventEntity = await _db.Events.FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (eventEntity is null)
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Event does not exist."));
        }

        var existing = await _db.Programs
            .FirstOrDefaultAsync(p => p.EventId == eventId, cancellationToken);
        if (existing is not null)
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.DuplicateName,
                    "This event already has a Program. Only one Program per Event is permitted."));
        }

        var entity = new Program
        {
            EventId = eventId,
            Title = title.Trim(),
        };
        _db.Programs.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Program>.Success(entity);
    }

    public async Task<ConfigurationResult<Program>> UpdateAsync(
        int programId,
        string title,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(title))
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Title is required."));
        }

        var entity = await _db.Programs.FirstOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Program), programId);
        }

        entity.Title = title.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Program>.Success(entity);
    }

    public async Task<ConfigurationResult<Program>> SoftDeleteAsync(
        int programId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.Programs
            .FirstOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Program), programId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Program is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(Program), programId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<Program>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<Program>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(Program),
            EntityId = entity.ProgramId,
            EntityName = entity.Title,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<Program>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Program>.Success(entity);
    }
}
