namespace MknMs.Application.Processes.Configuration.ConfigureServiceDutyAndSchedule;

/// <summary>
/// Subprocess 1.6 — Configure Service Duty and Schedule.
/// </summary>
/// <remarks>
/// Manages two related join entities:
///   - ServiceDefinitionDuty — a duty required by a service, with a
///     slot count. Composite key (ServiceDefId, DutyId).
///   - ServiceSchedule — an instance of a service at a specific time
///     slot, with a start time. Uniqueness on (ServiceDefId, TimeSlotId)
///     among active rows.
///
/// The OwningBranchId rule on ServiceDefinition is enforced here — at
/// schedule creation — per §9.1.6.
///
/// Specification: §4 (Service Definition Duty, Service Schedule),
/// §9.1.4, §9.1.5, §9.1.6, §15.2.3.
/// </remarks>
public interface IConfigureServiceDutyAndScheduleService
{
    Task<IReadOnlyList<ServiceDefinitionDuty>> ListDutiesAsync(
        int serviceDefId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinitionDuty>> AddDutyAsync(
        int serviceDefId,
        int dutyId,
        int requiredSlotCount,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinitionDuty>> UpdateDutyAsync(
        int serviceDefId,
        int dutyId,
        int requiredSlotCount,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinitionDuty>> RemoveDutyAsync(
        int serviceDefId,
        int dutyId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceSchedule>> ListSchedulesAsync(
        int? serviceDefId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceSchedule>> CreateScheduleAsync(
        int serviceDefId,
        int timeSlotId,
        TimeOnly startTime,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceSchedule>> UpdateScheduleAsync(
        int scheduleId,
        TimeOnly startTime,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceSchedule>> DeactivateScheduleAsync(
        int scheduleId,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureServiceDutyAndScheduleService : IConfigureServiceDutyAndScheduleService
{
    private readonly MknDbContext _db;

    public ConfigureServiceDutyAndScheduleService(MknDbContext db)
    {
        _db = db;
    }

    // ---------------------------------------------------------------------
    // Service Definition Duty
    // ---------------------------------------------------------------------

    public async Task<IReadOnlyList<ServiceDefinitionDuty>> ListDutiesAsync(
        int serviceDefId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ServiceDefinitionDuties
            .Where(d => d.ServiceDefId == serviceDefId);
        if (!includeInactive)
        {
            query = query.Where(d => d.IsActive);
        }
        return await query.OrderBy(d => d.DutyId).ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<ServiceDefinitionDuty>> AddDutyAsync(
        int serviceDefId,
        int dutyId,
        int requiredSlotCount,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsPositive(requiredSlotCount))
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "RequiredSlotCount must be a positive integer."));
        }

        var definition = await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        if (definition is null)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Service definition does not exist."));
        }

        var duty = await _db.Duties
            .FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (duty is null)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Duty does not exist."));
        }

        var existing = await _db.ServiceDefinitionDuties
            .FirstOrDefaultAsync(d => d.ServiceDefId == serviceDefId && d.DutyId == dutyId,
                cancellationToken);
        if (existing is not null)
        {
            if (existing.IsActive)
            {
                return ConfigurationResult<ServiceDefinitionDuty>.Failure(
                    new ConfigurationError(ConfigurationErrorCodes.DuplicateName,
                        "This duty is already required by this service."));
            }

            existing.IsActive = true;
            existing.RequiredSlotCount = requiredSlotCount;
            await _db.SaveChangesAsync(cancellationToken);
            return ConfigurationResult<ServiceDefinitionDuty>.Success(existing);
        }

        var entity = new ServiceDefinitionDuty
        {
            ServiceDefId = serviceDefId,
            DutyId = dutyId,
            RequiredSlotCount = requiredSlotCount,
            IsActive = true,
        };
        _db.ServiceDefinitionDuties.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinitionDuty>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceDefinitionDuty>> UpdateDutyAsync(
        int serviceDefId,
        int dutyId,
        int requiredSlotCount,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsPositive(requiredSlotCount))
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "RequiredSlotCount must be a positive integer."));
        }

        var entity = await _db.ServiceDefinitionDuties
            .FirstOrDefaultAsync(d => d.ServiceDefId == serviceDefId && d.DutyId == dutyId,
                cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceDefinitionDuty), serviceDefId);
        }

        entity.RequiredSlotCount = requiredSlotCount;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinitionDuty>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceDefinitionDuty>> RemoveDutyAsync(
        int serviceDefId,
        int dutyId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ServiceDefinitionDuties
            .FirstOrDefaultAsync(d => d.ServiceDefId == serviceDefId && d.DutyId == dutyId,
                cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceDefinitionDuty), serviceDefId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "This duty is already deactivated for this service."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceDefinitionDuty>.Success(entity);
    }

    // ---------------------------------------------------------------------
    // Service Schedule
    // ---------------------------------------------------------------------

    public async Task<IReadOnlyList<ServiceSchedule>> ListSchedulesAsync(
        int? serviceDefId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.ServiceSchedules.AsQueryable();
        if (serviceDefId is int id)
        {
            query = query.Where(s => s.ServiceDefId == id);
        }
        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }
        return await query.OrderBy(s => s.ScheduleId).ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<ServiceSchedule>> CreateScheduleAsync(
        int serviceDefId,
        int timeSlotId,
        TimeOnly startTime,
        CancellationToken cancellationToken = default)
    {
        var definition = await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        if (definition is null)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Service definition does not exist."));
        }

        if (!definition.IsActive)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityInactive,
                    "Service definition is inactive."));
        }

        var slot = await _db.BranchTimeSlots
            .FirstOrDefaultAsync(t => t.TimeSlotId == timeSlotId, cancellationToken);
        if (slot is null)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Time slot does not exist."));
        }

        if (!slot.IsActive)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityInactive,
                    "Time slot is inactive."));
        }

        if (definition.OwningBranchId is int owningBranchId
            && owningBranchId != slot.BranchId)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "This service is exclusive to a different branch than the time slot's branch."));
        }

        var existingActive = await _db.ServiceSchedules
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId
                && s.TimeSlotId == timeSlotId
                && s.IsActive,
                cancellationToken);
        if (existingActive is not null)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.DuplicateName,
                    "An active schedule already exists for this service and time slot."));
        }

        var entity = new ServiceSchedule
        {
            ServiceDefId = serviceDefId,
            TimeSlotId = timeSlotId,
            StartTime = startTime,
            IsActive = true,
        };
        _db.ServiceSchedules.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceSchedule>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceSchedule>> UpdateScheduleAsync(
        int scheduleId,
        TimeOnly startTime,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ServiceSchedules
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceSchedule), scheduleId);
        }

        entity.StartTime = startTime;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceSchedule>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceSchedule>> DeactivateScheduleAsync(
        int scheduleId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.ServiceSchedules
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceSchedule), scheduleId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "Schedule is already inactive."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceSchedule>.Success(entity);
    }
}
