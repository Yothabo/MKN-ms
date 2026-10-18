namespace MknMs.Application.Processes.Configuration.RecordReadmission;

/// <summary>
/// Inputs to one Record Readmission command.
/// </summary>
/// <remarks>
/// A readmission is a historical fact: one row per event, no
/// lifecycle flags, never removed through normal operations.
///
/// Specification: §4 (Readmission), §9.1.7, §13.6, §15.8.23;
/// docs/processes/contracts.md "Record Readmission command".
/// </remarks>
public sealed record RecordReadmissionCommand
{
    public required int MemberId { get; init; }

    public required DateOnly ReadmissionDate { get; init; }

    public string? Reason { get; init; }

    public required int ActingAdminId { get; init; }
}
