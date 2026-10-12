namespace MknMs.Application.Processes.Configuration.ConfigureTimeSlot;

/// <summary>
/// Subprocess 1.4 — Configure Time Slot.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists BranchTimeSlot rows. The slot
/// references a Branch and a TimeOfDay lookup. Multiple slots per
/// (BranchId, DayOfWeek) are permitted — no uniqueness constraint on
/// that pair.
///
/// Specification: §4 (Branch Time Slot), §9.1.3.
/// </remarks>
public interface IConfigureTimeSlotService
{
    Task<IReadOnlyList<BranchTimeSlot>> ListAsync(
        int? branchId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<BranchTimeSlot?> GetAsync(int timeSlotId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<BranchTimeSlot>> CreateAsync(
        int branchId,
        string dayOfWeek,
        int timeOfDayId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<BranchTimeSlot>> UpdateAsync(
        int timeSlotId,
        string dayOfWeek,
        int timeOfDayId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<BranchTimeSlot>> DeactivateAsync(
        int timeSlotId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureTimeSlotService : IConfigureTimeSlotService
{
    private static readonly string[] ValidDays =
    {
        "Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"
    };

    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;

    public ConfigureTimeSlotService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle)
    {
        _db = db;
        _lifecycle = lifecycle;
    }

    public async Task<IReadOnlyList<BranchTimeSlot>> ListAsync(
        int? branchId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.BranchTimeSlots.AsQueryable();
        if (branchId is int id)
        {
            query = query.Where(t => t.BranchId == id);
        }
        if (!includeInactive)
        {
            query = query.Where(t => t.IsActive);
        }
        return await query.OrderBy(t => t.TimeSlotId).ToListAsync(cancellationToken);
    }

    public async Task<BranchTimeSlot?> GetAsync(int timeSlotId, CancellationToken cancellationToken = default)
    {
        return await _db.BranchTimeSlots.FirstOrDefaultAsync(t => t.TimeSlotId == timeSlotId, cancellationToken);
    }

    public async Task<ConfigurationResult<BranchTimeSlot>> CreateAsync(
        int branchId,
        string dayOfWeek,
        int timeOfDayId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
        if (branch is null)
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing, "Branch does not exist."));
        }

        if (!ValidDays.Contains(dayOfWeek))
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "DayOfWeek must be a valid weekday name."));
        }

        var timeOfDay = await _db.TimeOfDays.FirstOrDefaultAsync(t => t.TimeOfDayId == timeOfDayId, cancellationToken);
        if (timeOfDay is null)
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing, "TimeOfDay does not exist."));
        }

        var entity = new BranchTimeSlot
        {
            BranchId = branchId,
            DayOfWeek = dayOfWeek,
            TimeOfDayId = timeOfDayId,
            IsActive = true,
        };
        _db.BranchTimeSlots.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<BranchTimeSlot>.Success(entity);
    }

    public async Task<ConfigurationResult<BranchTimeSlot>> UpdateAsync(
        int timeSlotId,
        string dayOfWeek,
        int timeOfDayId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        if (!ValidDays.Contains(dayOfWeek))
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "DayOfWeek must be a valid weekday name."));
        }

        var entity = await _db.BranchTimeSlots.FirstOrDefaultAsync(t => t.TimeSlotId == timeSlotId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(BranchTimeSlot), timeSlotId);
        }

        var timeOfDay = await _db.TimeOfDays.FirstOrDefaultAsync(t => t.TimeOfDayId == timeOfDayId, cancellationToken);
        if (timeOfDay is null)
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing, "TimeOfDay does not exist."));
        }

        entity.DayOfWeek = dayOfWeek;
        entity.TimeOfDayId = timeOfDayId;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<BranchTimeSlot>.Success(entity);
    }

    public async Task<ConfigurationResult<BranchTimeSlot>> DeactivateAsync(
        int timeSlotId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.BranchTimeSlots.FirstOrDefaultAsync(t => t.TimeSlotId == timeSlotId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(BranchTimeSlot), timeSlotId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Time slot is already inactive."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(BranchTimeSlot),
            EntityId = entity.TimeSlotId,
            EntityName = $"{entity.DayOfWeek} {entity.TimeSlotId}",
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<BranchTimeSlot>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<BranchTimeSlot>.Success(entity);
    }
}
