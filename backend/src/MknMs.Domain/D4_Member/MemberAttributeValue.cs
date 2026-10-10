
namespace MknMs.Domain.D4_Member;

/// <summary>
/// One value for one member on one attribute type.
/// </summary>
/// <remarks>
/// Unique on (MemberId, AttributeTypeId). Absence of a row means the
/// attribute is not set.
///
/// Specification: §4 (Member Attribute Value), §15.8.5.
/// </remarks>
public class MemberAttributeValue
{
    public int MemberAttributeValueId { get; set; }

    public int MemberId { get; set; }

    public int AttributeTypeId { get; set; }

    public string Value { get; set; } = null!;

    public DateOnly RecordedDate { get; set; }

    public int RecordedBy { get; set; }

    // Navigation
    public Member Member { get; set; } = null!;

    public AttributeType AttributeType { get; set; } = null!;

    public Admin RecordedByAdmin { get; set; } = null!;
}
