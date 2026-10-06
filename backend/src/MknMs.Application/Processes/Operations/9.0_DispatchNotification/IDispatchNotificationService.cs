namespace MknMs.Application.Processes.Operations.DispatchNotification;

/// <summary>
/// The Dispatch Notification process (9.0).
/// </summary>
/// <remarks>
/// Invoked by 5.0 Generate Assignment and 7.0 Manage Confirmation when
/// they create a new assignment. It is one of the two direct process
/// invocation edges in the system; everything else is data-mediated.
///
/// The invocation is fire-and-forget from the invoking process's
/// perspective. Whether the actual dispatch is synchronous or
/// asynchronous is an implementation choice and does not create a
/// semantic dependency (see §16.6).
///
/// This interface is the contract. The concrete implementation is
/// delivered when 9.0 is implemented. Until then, a no-op stub
/// satisfies the interface so that 5.0 can be tested in isolation.
///
/// Specification: §14, §16.2.
/// </remarks>
public interface IDispatchNotificationService
{
    /// <summary>
    /// Sends the assignment notice for a newly created assignment.
    /// Called exactly once per new assignment, at the moment the
    /// assignment is created.
    /// </summary>
    Task DispatchAssignmentNoticeAsync(
        RosterAssignment assignment,
        CancellationToken cancellationToken = default);
}
