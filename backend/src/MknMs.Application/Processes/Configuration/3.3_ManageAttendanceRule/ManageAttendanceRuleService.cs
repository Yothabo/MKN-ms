
namespace MknMs.Application.Processes.Configuration.ManageAttendanceRule;

/// <summary>
/// Subprocess 3.3 — Manage Attendance Rule.
/// </summary>
/// <remarks>
/// Creates, edits, disables, deactivates, and soft-deletes
/// AttendanceRule rows and their AttendanceRuleScope children. The
/// three lifecycle mechanisms are distinct:
///
///   - Disable sets Enabled = false. The rule is not evaluated, but
///     IsActive and the scope rows are unchanged.
///   - Deactivate sets IsActive = false. The rule is not evaluated;
///     scope rows remain.
///   - Soft-delete sets IsDeleted = true (and implicitly
///     IsActive = false). The rule's scope rows are removed.
///
/// TriggerType is one of AbsenceDays, ReadmissionCount, Manual.
/// OutcomeType is one of Notify, SetStatus, NoOp. OutcomeStatusId is
/// set only when OutcomeType = SetStatus. Scope rows use the Duty
/// Rule criteria vocabulary.
///
/// Specification: §4 (Attendance Rule, Attendance Rule Scope),
/// §9.7, §13.6, §15.8.21, §15.8.22, §15.8.34.
/// </remarks>
public interface IManageAttendanceRuleService
{
    Task<IReadOnlyList<AttendanceRule>> ListAsync(
        bool includeDisabled = true,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<AttendanceRule?> GetAsync(
        int ruleId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AttendanceRuleScope>> ListScopeAsync(
        int ruleId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttendanceRule>> CreateAsync(
        CreateAttendanceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttendanceRule>> UpdateAsync(
        int ruleId,
        UpdateAttendanceRuleRequest request,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttendanceRule>> DisableAsync(
        int ruleId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttendanceRule>> EnableAsync(
        int ruleId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttendanceRule>> DeactivateAsync(
        int ruleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttendanceRule>> SoftDeleteAsync(
        int ruleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed record AttendanceRuleScopeInput
{
    public required string CriteriaType { get; init; }
    public required string CriteriaValue { get; init; }
}

public sealed record CreateAttendanceRuleRequest
{
    public required string Name { get; init; }
    public required string TriggerType { get; init; }
    public string? TriggerValue { get; init; }
    public required string OutcomeType { get; init; }
    public int? OutcomeStatusId { get; init; }
    public required bool Enabled { get; init; }
    public IReadOnlyList<AttendanceRuleScopeInput>? ScopeCriteria { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record UpdateAttendanceRuleRequest
{
    public required string Name { get; init; }
    public required string TriggerType { get; init; }
    public string? TriggerValue { get; init; }
    public required string OutcomeType { get; init; }
    public int? OutcomeStatusId { get; init; }
    public required bool Enabled { get; init; }
    public IReadOnlyList<AttendanceRuleScopeInput>? ScopeCriteria { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed class ManageAttendanceRuleService : IManageAttendanceRuleService
{
    private static readonly HashSet<string> ValidTriggerTypes = new(StringComparer.Ordinal)
    {
        "AbsenceDays",
        "ReadmissionCount",
        "Manual",
    };

    private static readonly HashSet<string> ValidOutcomeTypes = new(StringComparer.Ordinal)
    {
        "Notify",
        "SetStatus",
        "NoOp",
    };

    private static readonly HashSet<string> ValidScopeCriteria = new(StringComparer.Ordinal)
    {
        "Role",
        "Gender",
        "AgeRange",
        "Tenure",
        "MembershipStage",
        "MemberStatus",
        "MemberAttribute",
        "Youth",
    };

    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageAttendanceRuleService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<IReadOnlyList<AttendanceRule>> ListAsync(
        bool includeDisabled = true,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.AttendanceRules
            .Where(r => !r.IsDeleted);

        if (!includeDisabled)
        {
            query = query.Where(r => r.Enabled);
        }
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query.OrderBy(r => r.AttendanceRuleId).ToListAsync(cancellationToken);
    }

    public async Task<AttendanceRule?> GetAsync(
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AttendanceRules
            .FirstOrDefaultAsync(r => r.AttendanceRuleId == ruleId, cancellationToken);
    }

    public async Task<IReadOnlyList<AttendanceRuleScope>> ListScopeAsync(
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        return await _db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId)
            .OrderBy(s => s.CriteriaType)
            .ThenBy(s => s.CriteriaValue)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<AttendanceRule>> CreateAsync(
        CreateAttendanceRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRequestAsync(
            request.Name, request.TriggerType, request.TriggerValue,
            request.OutcomeType, request.OutcomeStatusId,
            request.ScopeCriteria, request.ActingAdminId, cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<AttendanceRule>.Failure(validation);
        }

        var entity = new AttendanceRule
        {
            Name = request.Name.Trim(),
            TriggerType = request.TriggerType,
            TriggerValue = string.IsNullOrWhiteSpace(request.TriggerValue)
                ? null : request.TriggerValue.Trim(),
            OutcomeType = request.OutcomeType,
            OutcomeStatusId = request.OutcomeType == "SetStatus"
                ? request.OutcomeStatusId : null,
            Enabled = request.Enabled,
            IsActive = true,
            IsDeleted = false,
        };
        _db.AttendanceRules.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        await ReplaceScopeAsync(entity.AttendanceRuleId, request.ScopeCriteria, cancellationToken);

        return ConfigurationResult<AttendanceRule>.Success(entity);
    }

    public async Task<ConfigurationResult<AttendanceRule>> UpdateAsync(
        int ruleId,
        UpdateAttendanceRuleRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateRequestAsync(
            request.Name, request.TriggerType, request.TriggerValue,
            request.OutcomeType, request.OutcomeStatusId,
            request.ScopeCriteria, request.ActingAdminId, cancellationToken);
        if (validation is not null)
        {
            return ConfigurationResult<AttendanceRule>.Failure(validation);
        }

        var entity = await _db.AttendanceRules
            .FirstOrDefaultAsync(r => r.AttendanceRuleId == ruleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(AttendanceRule), ruleId);
        }

        entity.Name = request.Name.Trim();
        entity.TriggerType = request.TriggerType;
        entity.TriggerValue = string.IsNullOrWhiteSpace(request.TriggerValue)
            ? null : request.TriggerValue.Trim();
        entity.OutcomeType = request.OutcomeType;
        entity.OutcomeStatusId = request.OutcomeType == "SetStatus"
            ? request.OutcomeStatusId : null;
        entity.Enabled = request.Enabled;
        await _db.SaveChangesAsync(cancellationToken);

        await ReplaceScopeAsync(entity.AttendanceRuleId, request.ScopeCriteria, cancellationToken);

        return ConfigurationResult<AttendanceRule>.Success(entity);
    }

    public async Task<ConfigurationResult<AttendanceRule>> DisableAsync(
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.AttendanceRules
            .FirstOrDefaultAsync(r => r.AttendanceRuleId == ruleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(AttendanceRule), ruleId);
        }

        if (!entity.Enabled)
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "Attendance Rule is already disabled."));
        }

        entity.Enabled = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<AttendanceRule>.Success(entity);
    }

    public async Task<ConfigurationResult<AttendanceRule>> EnableAsync(
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.AttendanceRules
            .FirstOrDefaultAsync(r => r.AttendanceRuleId == ruleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(AttendanceRule), ruleId);
        }

        if (entity.Enabled)
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyActive,
                    "Attendance Rule is already enabled."));
        }

        entity.Enabled = true;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<AttendanceRule>.Success(entity);
    }

    public async Task<ConfigurationResult<AttendanceRule>> DeactivateAsync(
        int ruleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.AttendanceRules
            .FirstOrDefaultAsync(r => r.AttendanceRuleId == ruleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(AttendanceRule), ruleId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "Attendance Rule is already inactive."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(AttendanceRule),
            EntityId = entity.AttendanceRuleId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<AttendanceRule>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<AttendanceRule>.Success(entity);
    }

    public async Task<ConfigurationResult<AttendanceRule>> SoftDeleteAsync(
        int ruleId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.AttendanceRules
            .FirstOrDefaultAsync(r => r.AttendanceRuleId == ruleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(AttendanceRule), ruleId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "Attendance Rule is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(AttendanceRule), ruleId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<AttendanceRule>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<AttendanceRule>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(AttendanceRule),
            EntityId = entity.AttendanceRuleId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<AttendanceRule>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<AttendanceRule>.Success(entity);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private async Task ReplaceScopeAsync(
        int ruleId,
        IReadOnlyList<AttendanceRuleScopeInput>? inputs,
        CancellationToken cancellationToken)
    {
        var existing = await _db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId)
            .ToListAsync(cancellationToken);
        _db.AttendanceRuleScopes.RemoveRange(existing);

        if (inputs is not null)
        {
            foreach (var input in inputs)
            {
                _db.AttendanceRuleScopes.Add(new AttendanceRuleScope
                {
                    AttendanceRuleId = ruleId,
                    CriteriaType = input.CriteriaType,
                    CriteriaValue = input.CriteriaValue,
                });
            }
        }

        await _db.SaveChangesAsync(cancellationToken);
    }

    private async Task<ConfigurationError?> ValidateRequestAsync(
        string name,
        string triggerType,
        string? triggerValue,
        string outcomeType,
        int? outcomeStatusId,
        IReadOnlyList<AttendanceRuleScopeInput>? scopeCriteria,
        int actingAdminId,
        CancellationToken cancellationToken)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required.");
        }

        if (!ValidTriggerTypes.Contains(triggerType))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                $"TriggerType '{triggerType}' is not in the supported vocabulary.");
        }

        if (triggerType != "Manual")
        {
            if (string.IsNullOrWhiteSpace(triggerValue))
            {
                return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    $"TriggerValue is required for TriggerType = {triggerType}.");
            }

            if (!int.TryParse(triggerValue, out var tv) || tv < 0)
            {
                return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    $"TriggerValue must be a non-negative integer for TriggerType = {triggerType}.");
            }
        }

        if (!ValidOutcomeTypes.Contains(outcomeType))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                $"OutcomeType '{outcomeType}' is not in the supported vocabulary.");
        }

        if (outcomeType == "SetStatus")
        {
            if (outcomeStatusId is null)
            {
                return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "OutcomeStatusId is required when OutcomeType = SetStatus.");
            }

            var statusExists = await _db.MemberStatuses
                .AnyAsync(s => s.MemberStatusId == outcomeStatusId.Value, cancellationToken);
            if (!statusExists)
            {
                return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "OutcomeStatus does not exist.");
            }
        }

        if (scopeCriteria is not null)
        {
            foreach (var input in scopeCriteria)
            {
                if (!ValidScopeCriteria.Contains(input.CriteriaType))
                {
                    return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                        $"Scope CriteriaType '{input.CriteriaType}' is not in the supported vocabulary.");
                }

                if (!ConfigurationValidation.IsValidName(input.CriteriaValue))
                {
                    return new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                        "Scope CriteriaValue is required.");
                }
            }
        }

        var admin = await _db.Admins
            .FirstOrDefaultAsync(a => a.AdminId == actingAdminId, cancellationToken);
        if (admin is null)
        {
            return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                "Acting administrator does not exist.");
        }

        return null;
    }
}
