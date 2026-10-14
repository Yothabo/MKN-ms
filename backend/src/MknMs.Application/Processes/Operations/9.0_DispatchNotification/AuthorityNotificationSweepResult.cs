
namespace MknMs.Application.Processes.Operations.DispatchNotification;

/// <summary>
/// Summary of one authority-notification sweep run.
/// </summary>
/// <remarks>
/// AuditRowsProcessed counts rows the sweep took out of the pending
/// set — either because every dispatch succeeded, or because zero
/// recipients resolved. RowsLeftForRetry counts rows whose dispatch
/// attempt failed and whose NotifiedAt was therefore left null.
///
/// NotificationsDispatched counts individual recipient dispatches,
/// not rows.
///
/// Specification: §14.1.3, §14.1.4, §14.1.9.
/// </remarks>
public sealed record AuthorityNotificationSweepResult
{
    public required string Status { get; init; }

    public required int AuditRowsProcessed { get; init; }

    public required int NotificationsDispatched { get; init; }

    public required int RowsLeftForRetry { get; init; }

    public string? ErrorCode { get; init; }

    public string? ErrorDetail { get; init; }
}
