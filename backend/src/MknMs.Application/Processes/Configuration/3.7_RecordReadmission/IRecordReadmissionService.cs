namespace MknMs.Application.Processes.Configuration.RecordReadmission;

/// <summary>
/// Subprocess 3.7 — Record Readmission.
/// </summary>
/// <remarks>
/// Creates one Readmission row for one member. §9.1.7: process 3.0
/// owns Readmission creation; 13.0 reads Readmission and never writes
/// it. The record is a historical fact, so the service does not write
/// a ConfigurationAuditLog row — §9.7 scopes that log to deactivations
/// and soft deletes.
///
/// Specification: §4 (Readmission), §9.1.7, §13.6, §15.8.23;
/// docs/processes/contracts.md "Record Readmission command".
/// </remarks>
public interface IRecordReadmissionService
{
    Task<ConfigurationResult<Readmission>> RecordAsync(
        RecordReadmissionCommand command,
        CancellationToken cancellationToken = default);
}
