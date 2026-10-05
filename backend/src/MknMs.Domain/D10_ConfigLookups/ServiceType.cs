namespace MknMs.Domain.D10_ConfigLookups;

/// <summary>
/// An administrator-defined classification of a service definition.
/// </summary>
/// <remarks>
/// Specification: §4 (lookups), §9.1.4, §9.1.12.
/// </remarks>
public class ServiceType
{
    public int ServiceTypeId { get; set; }

    public string Name { get; set; } = null!;
}
