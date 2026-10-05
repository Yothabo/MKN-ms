namespace MknMs.Domain.D3_BranchTimeSlotService;

/// <summary>
/// A duty required by a service definition, with a slot count.
/// The join of ServiceDefinition and Duty.
/// </summary>
/// <remarks>
/// Composite primary key on (ServiceDefId, DutyId). RequiredSlotCount
/// is administrator-set with no default. The entity inherits the
/// deactivation model: a row may be deactivated to stop the duty being
/// required on future occurrences, without affecting existing ones.
///
/// Specification: §4 (Service Definition Duty), §9.1.4.
/// </remarks>
public class ServiceDefinitionDuty
{
    public int ServiceDefId { get; set; }

    public int DutyId { get; set; }

    public int RequiredSlotCount { get; set; }

    public bool IsActive { get; set; } = true;

    // Navigation
    public ServiceDefinition ServiceDefinition { get; set; } = null!;

    public Duty Duty { get; set; } = null!;
}
