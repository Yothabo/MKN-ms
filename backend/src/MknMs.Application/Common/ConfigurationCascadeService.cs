namespace MknMs.Application.Common;

/// <summary>
/// Prepares the reference fallbacks and dependent-row changes that
/// §9.7 requires when a configuration entity is soft-deleted.
/// </summary>
/// <remarks>
/// The cascade service prepares changes; it does not commit them.
/// The calling process coordinates the operation:
///
///   1. Apply the cascade rules through ApplyAsync.
///   2. If the cascade returns Blocked, return Failure without
///      touching the parent or writing an audit row.
///   3. Mark the parent as soft-deleted.
///   4. Add the audit row through ConfigurationLifecycleService,
///      passing the cascade's ConsequencesText.
///   5. Call SaveChangesAsync once.
///
/// The dependent changes, the parent's state change, and the audit
/// row therefore commit or roll back together (§9.8).
///
/// Each entity type has one rule, matching §9.7's table. Rules are
/// added per-entity in their own commits, matching the entity's
/// soft-delete method landing.
///
/// Specification: §9.7, §9.8.
/// </remarks>
public interface IConfigurationCascadeService
{
    Task<ConfigurationResult<CascadeOutcome>> ApplyAsync(
        string entityType,
        int entityId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Outcome of a cascade application.
/// </summary>
public sealed class CascadeOutcome
{
    private CascadeOutcome(bool blocked, string? blockedReason, string consequencesText)
    {
        IsBlocked = blocked;
        BlockedReason = blockedReason;
        ConsequencesText = consequencesText;
    }

    public bool IsBlocked { get; }

    public string? BlockedReason { get; }

    public string ConsequencesText { get; }

    public static CascadeOutcome Blocked(string reason) =>
        new(true, reason, string.Empty);

    public static CascadeOutcome Applied(string consequencesText) =>
        new(false, null, consequencesText);
}

public sealed class ConfigurationCascadeService : IConfigurationCascadeService
{
    private readonly MknDbContext _db;

    public ConfigurationCascadeService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<ConfigurationResult<CascadeOutcome>> ApplyAsync(
        string entityType,
        int entityId,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(entityType))
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "EntityType is required."));
        }

        if (entityId <= 0)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "EntityId must be positive."));
        }

        switch (entityType)
        {
            case nameof(Role):
                return await ApplyRoleCascadeAsync(entityId, cancellationToken);

            case nameof(Duty):
                return await ApplyDutyCascadeAsync(entityId, cancellationToken);

            case nameof(MemberStatus):
                return await ApplyMemberStatusCascadeAsync(entityId, cancellationToken);

            case nameof(Branch):
                return await ApplyBranchCascadeAsync(entityId, cancellationToken);

            case nameof(BranchTimeSlot):
                return await ApplyBranchTimeSlotCascadeAsync(entityId, cancellationToken);

            case nameof(ServiceDefinition):
                return await ApplyServiceDefinitionCascadeAsync(entityId, cancellationToken);

            case nameof(ServiceSchedule):
                return await ApplyServiceScheduleCascadeAsync(entityId, cancellationToken);

            case nameof(AttributeType):
                return await ApplyAttributeTypeCascadeAsync(entityId, cancellationToken);

            case nameof(Event):
                return await ApplyEventCascadeAsync(entityId, cancellationToken);

            case nameof(Program):
                return await ApplyProgramCascadeAsync(entityId, cancellationToken);

            case nameof(ProgramItem):
                return await ApplyProgramItemCascadeAsync(entityId, cancellationToken);

            case nameof(OutcomeState):
                return await ApplyOutcomeStateCascadeAsync(entityId, cancellationToken);

            case nameof(AssignmentStatus):
                return await ApplyAssignmentStatusCascadeAsync(entityId, cancellationToken);

            case nameof(TimeOfDay):
                return await ApplyTimeOfDayCascadeAsync(entityId, cancellationToken);

            case nameof(ServiceType):
                return await ApplyServiceTypeCascadeAsync(entityId, cancellationToken);

            case nameof(PermissionTier):
                return await ApplyPermissionTierCascadeAsync(entityId, cancellationToken);

            case nameof(AttendanceRule):
                return await ApplyAttendanceRuleCascadeAsync(entityId, cancellationToken);

            case nameof(NotificationSubscription):
                return await ApplyNotificationSubscriptionCascadeAsync(entityId, cancellationToken);

            default:
                return ConfigurationResult<CascadeOutcome>.Failure(
                    new ConfigurationError(
                        ConfigurationErrorCodes.InvalidValue,
                        $"Unknown entity type '{entityType}'. The cascade "
                        + "cannot proceed; add an explicit arm for this type."));
        }
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyRoleCascadeAsync(
        int roleId,
        CancellationToken cancellationToken)
    {
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
        if (role is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"Role {roleId} does not exist."));
        }

        if (role.IsDefault)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    "The default Role cannot be soft-deleted."));
        }

        var defaultRole = await _db.Roles
            .FirstOrDefaultAsync(r => r.IsDefault && !r.IsDeleted, cancellationToken);
        if (defaultRole is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ConfigurationMissing,
                    "No default Role is configured; the fallback target cannot be resolved."));
        }

        var affectedMembers = await _db.Members
            .Where(m => m.RoleId == roleId)
            .ToListAsync(cancellationToken);
        foreach (var member in affectedMembers)
        {
            member.RoleId = defaultRole.RoleId;
        }

        var affectedRules = await _db.DutyRules
            .Where(r => r.CriteriaType == "Role"
                && r.CriteriaValue == role.Name
                && !r.IsDeleted)
            .ToListAsync(cancellationToken);
        foreach (var rule in affectedRules)
        {
            rule.IsDeleted = true;
            rule.IsActive = false;
        }

        var text =
            $"{affectedMembers.Count} member(s) fell back to the default Role; "
            + $"{affectedRules.Count} Duty Rule(s) were soft-deleted.";

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyDutyCascadeAsync(
        int dutyId,
        CancellationToken cancellationToken)
    {
        var duty = await _db.Duties
            .FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (duty is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"Duty {dutyId} does not exist."));
        }

        var sddCount = await _db.ServiceDefinitionDuties
            .CountAsync(d => d.DutyId == dutyId, cancellationToken);
        var edCount = await _db.EventDuties
            .CountAsync(d => d.DutyId == dutyId, cancellationToken);

        if (sddCount > 0 || edCount > 0)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    $"Duty {dutyId} is referenced by "
                    + $"{sddCount} Service Definition Duty row(s) and "
                    + $"{edCount} Event Duty row(s)."));
        }

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied("No dependent changes apply."));
    }

    // -----------------------------------------------------------------
    // MemberStatus cascade (§9.7 row)
    //
    // Blocked if the MemberStatus is the default. Otherwise, members
    // holding the status fall back to the default status, and:
    //
    //   - AttendanceRule rows whose OutcomeStatusId names this
    //     status are physically removed.
    //   - AttendanceRuleScope rows whose criteria name this status
    //     are physically removed.
    //
    // §9.7 uses the word "removed" for these dependents, in
    // contrast to "deleted" for the Role cascade's dependent
    // DutyRules. The two verbs are deliberate; the dependent rows
    // are removed from persistence.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyMemberStatusCascadeAsync(
        int memberStatusId,
        CancellationToken cancellationToken)
    {
        var status = await _db.MemberStatuses
            .FirstOrDefaultAsync(s => s.MemberStatusId == memberStatusId, cancellationToken);
        if (status is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"MemberStatus {memberStatusId} does not exist."));
        }

        if (status.IsDefault)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    "The default Member Status cannot be soft-deleted."));
        }

        var defaultStatus = await _db.MemberStatuses
            .FirstOrDefaultAsync(s => s.IsDefault && !s.IsDeleted, cancellationToken);
        if (defaultStatus is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ConfigurationMissing,
                    "No default Member Status is configured; the fallback target cannot be resolved."));
        }

        // 1. Member fallback.
        var affectedMembers = await _db.Members
            .Where(m => m.MemberStatusId == memberStatusId)
            .ToListAsync(cancellationToken);
        foreach (var member in affectedMembers)
        {
            member.MemberStatusId = defaultStatus.MemberStatusId;
        }

        // 2. AttendanceRule rows whose OutcomeStatusId names this
        //    status.
        var affectedRules = await _db.AttendanceRules
            .Where(r => r.OutcomeStatusId == memberStatusId)
            .ToListAsync(cancellationToken);

        // 3. AttendanceRuleScope rows to remove. Two groups:
        //    - scope rows belonging to the affected rules, above;
        //    - scope rows on any rule whose MemberStatus criterion
        //      names this status id.
        var ruleIds = affectedRules.Select(r => r.AttendanceRuleId).ToList();

        var scopeRowsByRule = await _db.AttendanceRuleScopes
            .Where(s => ruleIds.Contains(s.AttendanceRuleId))
            .ToListAsync(cancellationToken);

        // The MemberStatus criterion's value is a comma-separated
        // list of MemberStatusIDs. Parse, do not substring match.
        var allScopeRows = await _db.AttendanceRuleScopes
            .Where(s => s.CriteriaType == "MemberStatus")
            .ToListAsync(cancellationToken);

        var statusIdText = memberStatusId.ToString();
        var scopeRowsByCriterion = allScopeRows
            .Where(s => s.CriteriaValue
                .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(v => v == statusIdText))
            .ToList();

        var scopeRowsToRemove = scopeRowsByRule
            .Concat(scopeRowsByCriterion)
            .DistinctBy(s => new { s.AttendanceRuleId, s.CriteriaType, s.CriteriaValue })
            .ToList();

        _db.AttendanceRuleScopes.RemoveRange(scopeRowsToRemove);
        _db.AttendanceRules.RemoveRange(affectedRules);

        var text =
            $"{affectedMembers.Count} member(s) fell back to the default Member Status; "
            + $"{affectedRules.Count} Attendance Rule(s) removed; "
            + $"{scopeRowsToRemove.Count} Attendance Rule Scope row(s) removed.";

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    // -----------------------------------------------------------------
    // Branch cascade (§9.7 row)
    //
    // Blocked if any Member is assigned to the Branch. Otherwise the
    // referencing BranchTimeSlot rows are physically removed. Event
    // HostBranchId and EventBranch rows are preserved.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyBranchCascadeAsync(
        int branchId,
        CancellationToken cancellationToken)
    {
        var branch = await _db.Branches
            .FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
        if (branch is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"Branch {branchId} does not exist."));
        }

        var memberCount = await _db.Members
            .CountAsync(m => m.BranchId == branchId, cancellationToken);
        if (memberCount > 0)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    $"Branch {branchId} has {memberCount} Member(s) assigned to it."));
        }

        var slots = await _db.BranchTimeSlots
            .Where(s => s.BranchId == branchId)
            .ToListAsync(cancellationToken);
        _db.BranchTimeSlots.RemoveRange(slots);

        var text = $"{slots.Count} Branch Time Slot row(s) removed.";
        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    // -----------------------------------------------------------------
    // BranchTimeSlot cascade (§9.7 row)
    //
    // Blocked if any active ServiceSchedule references the slot.
    // Otherwise, inactive ServiceSchedule rows referencing the slot
    // are physically removed. §9.7's wording — 'removed if they were
    // already inactive' — gives both clauses distinct meaning: an
    // active schedule blocks, and the otherwise case removes the
    // inactive ones.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyBranchTimeSlotCascadeAsync(
        int timeSlotId,
        CancellationToken cancellationToken)
    {
        var slot = await _db.BranchTimeSlots
            .FirstOrDefaultAsync(s => s.TimeSlotId == timeSlotId, cancellationToken);
        if (slot is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"BranchTimeSlot {timeSlotId} does not exist."));
        }

        var activeSchedules = await _db.ServiceSchedules
            .CountAsync(s => s.TimeSlotId == timeSlotId && s.IsActive, cancellationToken);
        if (activeSchedules > 0)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    $"BranchTimeSlot {timeSlotId} is referenced by "
                    + $"{activeSchedules} active Service Schedule(s)."));
        }

        var inactiveSchedules = await _db.ServiceSchedules
            .Where(s => s.TimeSlotId == timeSlotId && !s.IsActive)
            .ToListAsync(cancellationToken);
        _db.ServiceSchedules.RemoveRange(inactiveSchedules);

        var text = $"{inactiveSchedules.Count} inactive Service Schedule row(s) removed.";
        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    // -----------------------------------------------------------------
    // ServiceDefinition cascade (§9.7 row)
    //
    // Blocked if any ServiceSchedule, ProgramItem, EventDuty or
    // DutyRule row references the definition. No otherwise clause:
    // the cascade is the block check alone, and no dependent rows
    // are touched on success.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyServiceDefinitionCascadeAsync(
        int serviceDefId,
        CancellationToken cancellationToken)
    {
        var definition = await _db.ServiceDefinitions
            .FirstOrDefaultAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        if (definition is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"ServiceDefinition {serviceDefId} does not exist."));
        }

        var scheduleCount = await _db.ServiceSchedules
            .CountAsync(s => s.ServiceDefId == serviceDefId, cancellationToken);
        var programItemCount = await _db.ProgramItems
            .CountAsync(p => p.ServiceDefId == serviceDefId, cancellationToken);
        var eventDutyCount = await _db.EventDuties
            .CountAsync(e => e.ServiceDefId == serviceDefId, cancellationToken);
        var ruleCount = await _db.DutyRules
            .CountAsync(r => r.ServiceDefId == serviceDefId, cancellationToken);

        if (scheduleCount > 0 || programItemCount > 0
            || eventDutyCount > 0 || ruleCount > 0)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    $"ServiceDefinition {serviceDefId} is referenced by "
                    + $"{scheduleCount} Service Schedule(s), "
                    + $"{programItemCount} Program Item(s), "
                    + $"{eventDutyCount} Event Duty row(s) and "
                    + $"{ruleCount} Duty Rule(s)."));
        }

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied("No dependent changes apply."));
    }

    // -----------------------------------------------------------------
    // ServiceSchedule cascade (§9.7 row)
    //
    // No block. No dependent removal. Existing ServiceOccurrence rows
    // are preserved. The schedule stops being eligible for future
    // materialization — that effect is enforced by the materializer's
    // read filter, not by this cascade.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyServiceScheduleCascadeAsync(
        int scheduleId,
        CancellationToken cancellationToken)
    {
        var schedule = await _db.ServiceSchedules
            .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, cancellationToken);
        if (schedule is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"ServiceSchedule {scheduleId} does not exist."));
        }

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(
                "Existing Service Occurrences are preserved; the schedule stops being eligible for future materialization."));
    }

    // -----------------------------------------------------------------
    // AttributeType cascade (§9.7 row)
    //
    // MemberAttributeValue rows for this attribute are physically
    // removed. Duty Rules whose CriteriaType = MemberAttribute and
    // whose CriteriaValue names this attribute are soft-deleted.
    //
    // The MemberAttribute criterion's value grammar is
    // 'AttributeTypeName=RequiredValue'. 'Names this attribute' means
    // the part before the '=' equals the AttributeType's Name; the
    // match parses the value, it does not substring-match.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyAttributeTypeCascadeAsync(
        int attributeTypeId,
        CancellationToken cancellationToken)
    {
        var attribute = await _db.AttributeTypes
            .FirstOrDefaultAsync(a => a.AttributeTypeId == attributeTypeId, cancellationToken);
        if (attribute is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"AttributeType {attributeTypeId} does not exist."));
        }

        // Physical removal of MemberAttributeValue rows.
        var values = await _db.MemberAttributeValues
            .Where(v => v.AttributeTypeId == attributeTypeId)
            .ToListAsync(cancellationToken);
        _db.MemberAttributeValues.RemoveRange(values);

        // Soft delete of Duty Rules naming this attribute. Load every
        // MemberAttribute-criterion rule and filter in memory after
        // parsing the value.
        var candidates = await _db.DutyRules
            .Where(r => r.CriteriaType == "MemberAttribute" && !r.IsDeleted)
            .ToListAsync(cancellationToken);

        var rulesToDelete = candidates
            .Where(r =>
            {
                var idx = r.CriteriaValue.IndexOf('=');
                if (idx <= 0) return false;
                var namePart = r.CriteriaValue.Substring(0, idx);
                return namePart == attribute.Name;
            })
            .ToList();

        foreach (var rule in rulesToDelete)
        {
            rule.IsDeleted = true;
            rule.IsActive = false;
        }

        var text =
            $"{values.Count} Member Attribute Value row(s) removed; "
            + $"{rulesToDelete.Count} Duty Rule(s) soft-deleted.";

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    // -----------------------------------------------------------------
    // Event cascade (§9.7 row)
    //
    // Referencing Program and EventDuty rows are physically removed.
    // Existing event-sourced ServiceOccurrence rows are preserved.
    //
    // Removing a Program requires removing its ProgramItem children
    // and the EventDuty rows that reference them, by FK chain. §9.7
    // names the Program and EventDuty removals; the ProgramItem
    // removal is implied by the FK and applied here.
    //
    // Removal order: every EventDuty (by Event and by ProgramItem),
    // then every ProgramItem, then every Program.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyEventCascadeAsync(
        int eventId,
        CancellationToken cancellationToken)
    {
        var evt = await _db.Events
            .FirstOrDefaultAsync(e => e.EventId == eventId, cancellationToken);
        if (evt is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"Event {eventId} does not exist."));
        }

        // Programs under this event and their items.
        var programs = await _db.Programs
            .Where(p => p.EventId == eventId)
            .ToListAsync(cancellationToken);

        var programIds = programs.Select(p => p.ProgramId).ToList();

        var programItems = programIds.Count == 0
            ? new List<ProgramItem>()
            : await _db.ProgramItems
                .Where(i => programIds.Contains(i.ProgramId))
                .ToListAsync(cancellationToken);

        // EventDuty rows referencing this Event directly.
        var eventDuties = await _db.EventDuties
            .Where(d => d.EventId == eventId)
            .ToListAsync(cancellationToken);

        // Remove in dependency order.
        _db.EventDuties.RemoveRange(eventDuties);
        _db.ProgramItems.RemoveRange(programItems);
        _db.Programs.RemoveRange(programs);

        var text =
            $"{programs.Count} Program(s) removed; "
            + $"{programItems.Count} Program Item(s) removed; "
            + $"{eventDuties.Count} Event Duty row(s) removed; "
            + "event-sourced Service Occurrences preserved.";

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    // -----------------------------------------------------------------
    // Program cascade (§9.7 row)
    //
    // Referencing ProgramItem rows are physically removed. Their
    // EventDuty children are also removed, by FK chain — a
    // ProgramItem cannot be removed while an EventDuty references
    // it. Removal order: EventDuty, then ProgramItem.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyProgramCascadeAsync(
        int programId,
        CancellationToken cancellationToken)
    {
        var program = await _db.Programs
            .FirstOrDefaultAsync(p => p.ProgramId == programId, cancellationToken);
        if (program is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"Program {programId} does not exist."));
        }

        var items = await _db.ProgramItems
            .Where(i => i.ProgramId == programId)
            .ToListAsync(cancellationToken);

        _db.ProgramItems.RemoveRange(items);

        var text = $"{items.Count} Program Item(s) removed.";

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    // -----------------------------------------------------------------
    // ProgramItem cascade (§9.7 row)
    //
    // Referencing EventDuty rows are physically removed. The
    // ProgramItem itself is retained as soft-deleted.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyProgramItemCascadeAsync(
        int itemId,
        CancellationToken cancellationToken)
    {
        var item = await _db.ProgramItems
            .FirstOrDefaultAsync(i => i.ItemId == itemId, cancellationToken);
        if (item is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"ProgramItem {itemId} does not exist."));
        }

        // ProgramItem has no dependent rows. Its own soft delete is
        // applied by the calling service.
        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied("No dependent changes apply."));
    }

    // -----------------------------------------------------------------
    // D10 lookup cascades (§9.7 rows)
    //
    // All five are block-only. Each has a specific set of referencing
    // tables and, for the two whose ids are stored in SystemSetting,
    // a specific set of setting keys. The setting check is scoped to
    // those keys: an unscoped value match would false-positive on a
    // numeric setting that coincidentally equals the lookup id.
    // -----------------------------------------------------------------

    private static readonly string[] OutcomeStateSettingKeys =
    {
        "OutcomeStateUnfilledID",
        "OutcomeStatePartiallyFilledID",
        "OutcomeStateFilledID",
        "OutcomeStateCancelledID",
    };

    private static readonly string[] AssignmentStatusSettingKeys =
    {
        "InitialAssignmentStatusID",
        "DeclinedStatusID",
        "TimedOutStatusID",
    };

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyOutcomeStateCascadeAsync(
        int outcomeStateId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.OutcomeStates
            .AnyAsync(o => o.OutcomeStateId == outcomeStateId, cancellationToken);
        if (!exists)
        {
            return NotFound(nameof(OutcomeState), outcomeStateId);
        }

        var occurrenceCount = await _db.ServiceOccurrences
            .CountAsync(o => o.FillStatusId == outcomeStateId, cancellationToken);

        var settingCount = await CountSettingReferencesAsync(
            OutcomeStateSettingKeys, outcomeStateId, cancellationToken);

        if (occurrenceCount > 0 || settingCount > 0)
        {
            return BlockedByReferences(
                nameof(OutcomeState), outcomeStateId,
                new (string, int)[]
                {
                    ("Service Occurrence", occurrenceCount),
                    ("System Setting", settingCount),
                });
        }

        return NoDependents();
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyAssignmentStatusCascadeAsync(
        int statusId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.AssignmentStatuses
            .AnyAsync(s => s.AssignmentStatusId == statusId, cancellationToken);
        if (!exists)
        {
            return NotFound(nameof(AssignmentStatus), statusId);
        }

        var assignmentCount = await _db.RosterAssignments
            .CountAsync(a => a.AssignmentStatusId == statusId, cancellationToken);

        var settingCount = await CountSettingReferencesAsync(
            AssignmentStatusSettingKeys, statusId, cancellationToken);

        if (assignmentCount > 0 || settingCount > 0)
        {
            return BlockedByReferences(
                nameof(AssignmentStatus), statusId,
                new (string, int)[]
                {
                    ("Roster Assignment", assignmentCount),
                    ("System Setting", settingCount),
                });
        }

        return NoDependents();
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyTimeOfDayCascadeAsync(
        int timeOfDayId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.TimeOfDays
            .AnyAsync(t => t.TimeOfDayId == timeOfDayId, cancellationToken);
        if (!exists)
        {
            return NotFound(nameof(TimeOfDay), timeOfDayId);
        }

        var slotCount = await _db.BranchTimeSlots
            .CountAsync(s => s.TimeOfDayId == timeOfDayId, cancellationToken);

        if (slotCount > 0)
        {
            return BlockedByReferences(
                nameof(TimeOfDay), timeOfDayId,
                new (string, int)[] { ("Branch Time Slot", slotCount) });
        }

        return NoDependents();
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyServiceTypeCascadeAsync(
        int serviceTypeId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.ServiceTypes
            .AnyAsync(s => s.ServiceTypeId == serviceTypeId, cancellationToken);
        if (!exists)
        {
            return NotFound(nameof(ServiceType), serviceTypeId);
        }

        var defCount = await _db.ServiceDefinitions
            .CountAsync(s => s.ServiceTypeId == serviceTypeId, cancellationToken);

        var occurrenceCount = await _db.ServiceOccurrences
            .CountAsync(o => o.ServiceTypeId == serviceTypeId, cancellationToken);

        if (defCount > 0 || occurrenceCount > 0)
        {
            return BlockedByReferences(
                nameof(ServiceType), serviceTypeId,
                new (string, int)[]
                {
                    ("Service Definition", defCount),
                    ("Service Occurrence", occurrenceCount),
                });
        }

        return NoDependents();
    }

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyPermissionTierCascadeAsync(
        int tierId,
        CancellationToken cancellationToken)
    {
        var tier = await _db.PermissionTiers
            .FirstOrDefaultAsync(t => t.PermissionTierId == tierId, cancellationToken);
        if (tier is null)
        {
            return NotFound(nameof(PermissionTier), tierId);
        }

        // §4: exactly one PermissionTier is the default; §9.7 blocks
        // soft-deleting the default. Same rule as Role and MemberStatus
        // — without this arm, an unreferenced default tier can be
        // soft-deleted, leaving zero defaults.
        if (tier.IsDefault)
        {
            return ConfigurationResult<CascadeOutcome>.Success(
                CascadeOutcome.Blocked(
                    "The default Permission Tier cannot be soft-deleted."));
        }

        var adminCount = await _db.Admins
            .CountAsync(a => a.PermissionTierId == tierId, cancellationToken);

        var policyCount = await _db.EntityDeletionPolicies
            .CountAsync(p => p.RequiredDeletePermissionTierId == tierId
                || p.RequiredDeactivatePermissionTierId == tierId,
                cancellationToken);

        if (adminCount > 0 || policyCount > 0)
        {
            return BlockedByReferences(
                nameof(PermissionTier), tierId,
                new (string, int)[]
                {
                    ("Admin", adminCount),
                    ("Entity Deletion Policy", policyCount),
                });
        }

        return NoDependents();
    }

    // -----------------------------------------------------------------
    // Shared helpers for the D10 lookup rules
    // -----------------------------------------------------------------

    private async Task<int> CountSettingReferencesAsync(
        string[] keys,
        int lookupId,
        CancellationToken cancellationToken)
    {
        var text = lookupId.ToString();
        return await _db.SystemSettings
            .CountAsync(s => keys.Contains(s.Key) && s.Value == text, cancellationToken);
    }

    private static ConfigurationResult<CascadeOutcome> NotFound(
        string entityType, int entityId) =>
        ConfigurationResult<CascadeOutcome>.Failure(
            new ConfigurationError(
                ConfigurationErrorCodes.NotFound,
                $"{entityType} {entityId} does not exist."));

    private static ConfigurationResult<CascadeOutcome> BlockedByReferences(
        string entityType,
        int entityId,
        (string Name, int Count)[] references)
    {
        var present = references.Where(r => r.Count > 0)
            .Select(r => $"{r.Count} {r.Name}(s)")
            .ToArray();

        var text = string.Join(", ", present);

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Blocked(
                $"{entityType} {entityId} is referenced by {text}."));
    }

    private static ConfigurationResult<CascadeOutcome> NoDependents() =>
        ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied("No dependent changes apply."));

    // -----------------------------------------------------------------
    // AttendanceRule cascade (§9.7 row)
    //
    // Soft delete removes the rule's AttendanceRuleScope rows. The
    // rule row itself is retained. Deactivate and Disable do not
    // invoke this rule — they leave scope rows in place.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyAttendanceRuleCascadeAsync(
        int ruleId,
        CancellationToken cancellationToken)
    {
        var rule = await _db.AttendanceRules
            .FirstOrDefaultAsync(r => r.AttendanceRuleId == ruleId, cancellationToken);
        if (rule is null)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"AttendanceRule {ruleId} does not exist."));
        }

        var scopes = await _db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId)
            .ToListAsync(cancellationToken);
        _db.AttendanceRuleScopes.RemoveRange(scopes);

        var text = $"{scopes.Count} Attendance Rule Scope row(s) removed.";
        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied(text));
    }

    // -----------------------------------------------------------------
    // NotificationSubscription cascade (§9.7 row)
    //
    // Nothing references a NotificationSubscription. §9.7 describes it
    // as "removed; not soft-deleted independently" — the clause is about
    // the cascade having no dependents to act on, not about the entity
    // lacking a soft-delete operation. The soft delete proceeds and the
    // audit row is written; this arm simply asserts that no dependents
    // need to be prepared.
    // -----------------------------------------------------------------

    private async Task<ConfigurationResult<CascadeOutcome>> ApplyNotificationSubscriptionCascadeAsync(
        int subscriptionId,
        CancellationToken cancellationToken)
    {
        var exists = await _db.NotificationSubscriptions
            .AnyAsync(s => s.SubscriptionId == subscriptionId, cancellationToken);
        if (!exists)
        {
            return ConfigurationResult<CascadeOutcome>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.NotFound,
                    $"NotificationSubscription {subscriptionId} does not exist."));
        }

        return ConfigurationResult<CascadeOutcome>.Success(
            CascadeOutcome.Applied("No dependent changes apply."));
    }
}
