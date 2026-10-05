namespace MknMs.Application.Processes.Operations.MaterializeOccurrences;

/// <summary>
/// The Materialize Occurrences process (11.0).
/// </summary>
/// <remarks>
/// Ensures ServiceOccurrence rows exist for every active ServiceSchedule
/// up to the configured horizon. Additive and idempotent — never
/// modifies or deletes an existing occurrence.
///
/// Specification: §7, §8, and docs/processes/operations/11.0-materialize-occurrences.md.
/// </remarks>
public interface IMaterializeOccurrencesService
{
    Task<MaterializeOccurrencesResult> RunAsync(
        MaterializeOccurrencesCommand command,
        CancellationToken cancellationToken = default);
}
