
namespace MknMs.Application.Processes.Operations.DispatchNotification;

/// <summary>
/// The authority-notification sweep of process 9.0, Path 2.
/// </summary>
/// <remarks>
/// §16.4: Path 2 is a scheduled trigger of 9.0, not an invocation.
/// The scheduler starts this service directly; no process calls it.
///
/// The sweep reads ConfigurationAuditLog rows whose NotifiedAt is
/// null, matches NotificationSubscription rows by EventType,
/// resolves recipients, dispatches one message per recipient
/// through the configured channel, and sets NotifiedAt.
///
/// The audit row is the retry unit: if any recipient's dispatch
/// fails, NotifiedAt is left null and the row is picked up on a
/// later sweep. A retry may notify some recipients again, which is
/// the at-least-once semantics §14.1.4 states.
///
/// Specification: §14.1.3, §14.1.4, §14.1.9, §16.4.
/// </remarks>
public interface IAuthorityNotificationSweepService
{
    Task<AuthorityNotificationSweepResult> RunAsync(
        CancellationToken cancellationToken = default);
}
