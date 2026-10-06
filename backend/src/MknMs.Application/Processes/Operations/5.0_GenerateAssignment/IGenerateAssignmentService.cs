namespace MknMs.Application.Processes.Operations.GenerateAssignment;

/// <summary>
/// The Generate Assignment process (5.0).
/// </summary>
/// <remarks>
/// Fills the required duty slots of a ServiceOccurrence by producing
/// RosterAssignment rows. Applies the two-mechanism model from §5:
///
///   - Eligibility gates candidacy.
///   - Duty Rule ranks candidates via tiers.
///
/// Additive — never modifies or removes an existing assignment. Fully
/// re-entrant — an interrupted run resumes on the next run. Invokes
/// 9.0 Dispatch Notification for each new assignment (the sole
/// outbound invocation this process performs).
///
/// Specification: §10, docs/processes/operations/5.0-generate-assignment.md.
/// </remarks>
public interface IGenerateAssignmentService
{
    Task<GenerateAssignmentResult> RunAsync(
        GenerateAssignmentCommand command,
        CancellationToken cancellationToken = default);
}
