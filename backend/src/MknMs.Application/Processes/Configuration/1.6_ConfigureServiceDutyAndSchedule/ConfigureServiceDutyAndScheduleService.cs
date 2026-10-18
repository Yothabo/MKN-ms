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
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinitionDuty>> UpdateDutyAsync(
        int serviceDefId,
        int dutyId,
        int requiredSlotCount,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceDefinitionDuty>> RemoveDutyAsync(
        int serviceDefId,
        int dutyId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ServiceSchedule>> ListSchedulesAsync(
        int? serviceDefId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceSchedule>> CreateScheduleAsync(
        int serviceDefId,
        int timeSlotId,
        TimeOnly startTime,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceSchedule>> UpdateScheduleAsync(
        int scheduleId,
        TimeOnly startTime,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceSchedule>> DeactivateScheduleAsync(
        int scheduleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<ServiceSchedule>> SoftDeleteScheduleAsync(
        int scheduleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureServiceDutyAndScheduleService : IConfigureServiceDutyAndScheduleService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ConfigureServiceDutyAndScheduleService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
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
            .Where(d => d.ServiceDefId == serviceDefId && !d.IsDeleted);
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
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(adminError);
        }

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
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(adminError);
        }

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
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(adminError);
        }

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

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(ServiceDefinitionDuty),
            EntityId = entity.ServiceDefId,
            EntityName = $"ServiceDef {entity.ServiceDefId} / Duty {entity.DutyId}",
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<ServiceDefinitionDuty>.Failure(audit.Error!);
        }

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
        var query = _db.ServiceSchedules.Where(s => !s.IsDeleted);
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
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(adminError);
        }

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
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(adminError);
        }

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
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(adminError);
        }

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

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;
        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(ServiceSchedule),
            EntityId = entity.ScheduleId,
            EntityName = $"Schedule {entity.ScheduleId}",
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceSchedule>.Success(entity);
    }

    public async Task<ConfigurationResult<ServiceSchedule>> SoftDeleteScheduleAsync(
        int scheduleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(adminError);
        }

        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.ServiceSchedules
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(ServiceSchedule), scheduleId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Schedule is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(ServiceSchedule), scheduleId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(ServiceSchedule),
            EntityId = entity.ScheduleId,
            EntityName = $"Schedule {entity.ScheduleId}",
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<ServiceSchedule>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<ServiceSchedule>.Success(entity);
    }
}
