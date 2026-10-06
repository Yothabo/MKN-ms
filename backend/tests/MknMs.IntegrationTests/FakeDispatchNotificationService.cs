using MknMs.Domain.D7_RosterAssignment;
using MknMs.Application.Processes.Operations.DispatchNotification;

namespace MknMs.IntegrationTests;

/// <summary>
/// A no-op implementation of IDispatchNotificationService for tests
/// that exercise processes which invoke 9.0 but do not test 9.0 itself.
/// </summary>
/// <remarks>
/// Records each invocation so the test can assert that a dispatch
/// occurred with the expected assignment. Does not read the database,
/// does not send anything, does not log. The real
/// DispatchNotificationService is exercised by its own tests.
///
/// The interface is the contract. This fake satisfies it the same way
/// the real implementation does — fire-and-forget, no observable side
/// effect beyond what the test records.
/// </remarks>
public sealed class FakeDispatchNotificationService : IDispatchNotificationService
{
    private readonly List<int> _dispatchedAssignmentIds = new();

    public IReadOnlyList<int> DispatchedAssignmentIds => _dispatchedAssignmentIds;

    public Task DispatchAssignmentNoticeAsync(
        RosterAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        _dispatchedAssignmentIds.Add(assignment.AssignmentId);
        return Task.CompletedTask;
    }
}
