
namespace MknMs.Domain.D2_DutyRule;

/// <summary>
/// A single criterion, at a single tier, for a single Duty. Multiple rows
/// may share the same TierOrder for a given Duty; those rows are ANDed.
/// </summary>
/// <remarks>
/// CriteriaType is constrained to the fixed vocabulary in §5 of the
/// specification. CriteriaValue is free text in storage, interpreted per
/// CriteriaType at evaluation time.
///
/// Specification: §4 (Duty Rule), §9.1.10, §9.7.
/// </remarks>
public class DutyRule
{
    public int RuleId { get; set; }

    public int DutyId { get; set; }

    public int TierOrder { get; set; }

    public string CriteriaType { get; set; } = null!;

    public string CriteriaValue { get; set; } = null!;

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public Duty Duty { get; set; } = null!;
}
