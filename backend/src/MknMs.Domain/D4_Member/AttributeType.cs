
namespace MknMs.Domain.D4_Member;

/// <summary>
/// An administrator-defined attribute name a member may carry a
/// value for.
/// </summary>
/// <remarks>
/// The system does not interpret any attribute name. Attributes are
/// configured by the administrator and used by Duty Rules whose
/// criteria type is Member Attribute.
///
/// Specification: §4 (Attribute Type), §5, §15.8.4.
/// </remarks>
public class AttributeType
{
    public int AttributeTypeId { get; set; }

    public string Name { get; set; } = null!;

    public string? Description { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }
}
