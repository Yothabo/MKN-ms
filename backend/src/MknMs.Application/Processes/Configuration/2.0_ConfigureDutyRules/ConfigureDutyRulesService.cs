namespace MknMs.Application.Processes.Configuration.ConfigureDutyRules;

/// <summary>
/// Process 2.0 — Configure Duty Rules.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists DutyRule rows. A Duty Rule
/// expresses a single criterion, at a single tier, for a single Duty.
/// Multiple rules may share the same TierOrder for a given Duty; the
/// engine ANDs them.
///
/// The criteria vocabulary is fixed by the specification §5. The
/// service validates CriteriaType against that vocabulary at write
/// time. CriteriaValue is free text and is interpreted by the engine
/// at evaluation time, not here.
///
/// The three reserved criteria types (AcceptanceRate, DutiesCarried,
/// DaysSinceLastAssignment) may be written but are not evaluated by
/// 5.2 until a future specification revision activates them. The
/// service accepts them as valid writes.
///
/// Specification: §4 (Duty Rule), §5, §9.1.10.
/// </remarks>
public interface IConfigureDutyRulesService
{
    Task<IReadOnlyList<DutyRule>> ListForDutyAsync(
        int dutyId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<DutyRule?> GetAsync(int ruleId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<DutyRule>> CreateAsync(
        int dutyId,
        int tierOrder,
        string criteriaType,
        string criteriaValue,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<DutyRule>> UpdateAsync(
        int ruleId,
        int tierOrder,
        string criteriaType,
        string criteriaValue,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<DutyRule>> DeactivateAsync(
        int ruleId,
        CancellationToken cancellationToken = default);
}

public sealed class ConfigureDutyRulesService : IConfigureDutyRulesService
{
    /// <summary>
    /// The fixed criteria vocabulary from §5. The service validates
    /// CriteriaType against this list at write time. Adding a new
    /// criteria type is a specification change that also updates this
    /// list.
    /// </summary>
    private static readonly HashSet<string> ValidCriteriaTypes = new(StringComparer.Ordinal)
    {
        // Active criteria types.
        "Role",
        "Gender",
        "AgeRange",
        "Tenure",
        "MembershipStage",
        "BranchAttendanceRecency",
        "EligibilityFlag",

        // Reserved for future use. Accepted as writes; not evaluated
        // by 5.2 until a future spec revision activates them.
        "AcceptanceRate",
        "DutiesCarried",
        "DaysSinceLastAssignment",
    };

    private readonly MknDbContext _db;

    public ConfigureDutyRulesService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<DutyRule>> ListForDutyAsync(
        int dutyId,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.DutyRules.Where(r => r.DutyId == dutyId);
        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }

        return await query
            .OrderBy(r => r.TierOrder)
            .ThenBy(r => r.RuleId)
            .ToListAsync(cancellationToken);
    }

    public async Task<DutyRule?> GetAsync(int ruleId, CancellationToken cancellationToken = default)
    {
        return await _db.DutyRules.FirstOrDefaultAsync(r => r.RuleId == ruleId, cancellationToken);
    }

    public async Task<ConfigurationResult<DutyRule>> CreateAsync(
        int dutyId,
        int tierOrder,
        string criteriaType,
        string criteriaValue,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsPositive(tierOrder))
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "TierOrder must be a positive integer."));
        }

        if (!ValidCriteriaTypes.Contains(criteriaType))
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    $"CriteriaType '{criteriaType}' is not in the supported vocabulary."));
        }

        if (!ConfigurationValidation.IsValidName(criteriaValue))
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "CriteriaValue is required."));
        }

        var duty = await _db.Duties
            .FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (duty is null)
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Duty does not exist."));
        }

        if (!duty.IsActive)
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityInactive,
                    "Duty is inactive."));
        }

        var entity = new DutyRule
        {
            DutyId = dutyId,
            TierOrder = tierOrder,
            CriteriaType = criteriaType,
            CriteriaValue = criteriaValue.Trim(),
            IsActive = true,
        };
        _db.DutyRules.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<DutyRule>.Success(entity);
    }

    public async Task<ConfigurationResult<DutyRule>> UpdateAsync(
        int ruleId,
        int tierOrder,
        string criteriaType,
        string criteriaValue,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsPositive(tierOrder))
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "TierOrder must be a positive integer."));
        }

        if (!ValidCriteriaTypes.Contains(criteriaType))
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    $"CriteriaType '{criteriaType}' is not in the supported vocabulary."));
        }

        if (!ConfigurationValidation.IsValidName(criteriaValue))
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "CriteriaValue is required."));
        }

        var entity = await _db.DutyRules
            .FirstOrDefaultAsync(r => r.RuleId == ruleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(DutyRule), ruleId);
        }

        // Editing a rule takes effect on the next 5.0 run.
        // Existing RosterAssignment rows are not rewritten — the
        // no-backward-propagation rule (§9.1.10) applies.
        entity.TierOrder = tierOrder;
        entity.CriteriaType = criteriaType;
        entity.CriteriaValue = criteriaValue.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<DutyRule>.Success(entity);
    }

    public async Task<ConfigurationResult<DutyRule>> DeactivateAsync(
        int ruleId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.DutyRules
            .FirstOrDefaultAsync(r => r.RuleId == ruleId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(DutyRule), ruleId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<DutyRule>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "Duty rule is already inactive."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<DutyRule>.Success(entity);
    }
}
