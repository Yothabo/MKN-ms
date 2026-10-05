namespace MknMs.Domain.D1_RoleDuty;

/// <summary>
/// A named responsibility that may be required of a service. An
/// administrator-defined label.
/// </summary>
/// <remarks>
/// Carries no reference to Role. The relationship — when one exists —
/// is expressed by a DutyRule row written by process 2.0.
///
/// Specification: §4 (Duty), §9.1.2, §9.1.1.
/// </remarks>
public class Duty
{
    public int DutyId { get; set; }

    public string Name { get; set; } = null!;

    public bool IsActive { get; set; } = true;
}
