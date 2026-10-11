namespace MknMs.Application.Processes.Operations.DispatchNotification;

/// <summary>
/// The Dispatch Notification process (9.0).
/// </summary>
/// <remarks>
/// Path 1 (member-facing): invoked by 5.0, 7.0, 12.0 and 13.0 under
/// the conditions §14.1.3 states. 9.0 is the sole process any other
/// process is permitted to invoke.
///
/// The invocation is fire-and-forget from the invoking process's
/// perspective. Whether the actual dispatch is synchronous or
/// asynchronous is an implementation choice and does not create a
/// semantic dependency (see §16.6).
///
/// Two member-facing methods exist, one per kind of outbound event:
///   - DispatchAssignmentNoticeAsync — an assignment was created.
///   - DispatchAttendanceRuleNoticeAsync — an Attendance Rule with
///     OutcomeType = Notify fired.
///
/// Specification: §14, §16.2.
/// </remarks>
public interface IDispatchNotificationService
{
    /// <summary>
    /// Sends the assignment notice for a newly created assignment.
    /// Called exactly once per new assignment, at the moment the
    /// assignment is created. Invoked by 5.0, 7.0, and 12.0.
    /// </summary>
    Task DispatchAssignmentNoticeAsync(
        RosterAssignment assignment,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends the notice for an Attendance Rule whose OutcomeType is
    /// Notify. Called by 13.0 when such a rule's trigger fires.
    /// </summary>
    Task DispatchAttendanceRuleNoticeAsync(
        Member member,
        AttendanceRule rule,
        CancellationToken cancellationToken = default);
}
