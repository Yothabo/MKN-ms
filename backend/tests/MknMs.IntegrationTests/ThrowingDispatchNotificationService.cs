using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D13_AttendanceRule;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D7_RosterAssignment;

namespace MknMs.IntegrationTests;

/// <summary>
/// An IDispatchNotificationService whose assignment-notice path throws.
/// Used to prove that a 9.0 invocation failure does not roll back the
/// assignment the invoking process has already committed (B7).
/// </summary>
/// <remarks>
/// The attendance-rule notice path is a no-op, matching the shape of
/// FakeDispatchNotificationService. Only the assignment-notice path is
/// made to fail, because only that path is invoked by 5.0.
/// </remarks>
public sealed class ThrowingDispatchNotificationService : IDispatchNotificationService
{
    public Task DispatchAssignmentNoticeAsync(
        RosterAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        throw new InvalidOperationException(
            "Simulated notification transport failure (test-only).");
    }

    public Task DispatchAttendanceRuleNoticeAsync(
        Member member,
        AttendanceRule rule,
        CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}
