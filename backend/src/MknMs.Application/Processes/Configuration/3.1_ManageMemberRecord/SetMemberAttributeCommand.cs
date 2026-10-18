namespace MknMs.Application.Processes.Configuration.ManageMemberRecord;

/// <summary>
/// Inputs to one Set Member Attribute command.
/// </summary>
/// <remarks>
/// Spec and contracts.md both describe the operation as a Set, not
/// an Insert: the (MemberId, AttributeTypeId) pair is unique per §4
/// and re-setting the pair replaces the value. The contract's
/// Conflict error text names the situation — "a member attribute
/// value already exists for the same pair and must be replaced" —
/// and the correct reading of that sentence is replace, not refuse.
///
/// Specification: §4 (Member Attribute Value), §9.1.7, §10.1.5;
/// docs/processes/contracts.md "Set Member Attribute command".
/// </remarks>
public sealed record SetMemberAttributeCommand
{
    public required int MemberId { get; init; }

    public required int AttributeTypeId { get; init; }

    public required string Value { get; init; }

    public required DateOnly RecordedDate { get; init; }

    public required int ActingAdminId { get; init; }
}
