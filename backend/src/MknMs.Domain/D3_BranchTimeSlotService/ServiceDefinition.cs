
namespace MknMs.Domain.D3_BranchTimeSlotService;

/// <summary>
/// A named template for a recurring service. An administrator-assigned
/// label, classified by an administrator-defined ServiceType, and
/// optionally exclusive to one branch.
/// </summary>
/// <remarks>
/// OwningBranchId null means the service is available to any branch.
/// Set means it is exclusive to that branch. Enforcement of this rule
/// happens when a ServiceSchedule is created or edited (process 1.6),
/// not at ServiceDefinition creation.
///
/// Specification: §4 (Service Definition), §9.1.4, §9.7.
/// </remarks>
public class ServiceDefinition
{
    public int ServiceDefId { get; set; }

    public string Name { get; set; } = null!;

    public int ServiceTypeId { get; set; }

    public int? OwningBranchId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public ServiceType ServiceType { get; set; } = null!;

    public Branch? OwningBranch { get; set; }
}
