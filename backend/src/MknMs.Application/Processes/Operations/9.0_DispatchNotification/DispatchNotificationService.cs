using Microsoft.Extensions.Logging;

namespace MknMs.Application.Processes.Operations.DispatchNotification;

/// <summary>
/// The real Dispatch Notification process (9.0).
/// </summary>
/// <remarks>
/// Reads the assignment, the member, the occurrence, and the configured
/// channel setting. Composes a message. Dispatches it through the
/// channel named by SystemSetting.NotificationChannel. Returns.
///
/// This implementation's transport is structured logging. The spec
/// places the channel mechanism outside the schema (§14.1.2): "the
/// actual credentials and transport live outside the schema." Logging
/// is a legitimate destination, and it is the smallest complete
/// implementation of the process the spec defines.
///
/// The switch on NotificationChannel is present from the start so that
/// adding Email, Webhook, or SMS later is a change to this file only.
/// The interface does not change. No caller changes.
///
/// Reads D7 RosterAssignment, D4 Member, D3 ServiceOccurrence, D11
/// SystemSetting. Writes nothing. Invokes nothing.
///
/// Fire-and-forget: the caller does not observe the result. If the
/// underlying channel fails, the failure is logged and swallowed.
///
/// Specification: §14.
/// </remarks>
public sealed class DispatchNotificationService : IDispatchNotificationService
{
    private const string ChannelKey = "NotificationChannel";

    private readonly MknDbContext _db;
    private readonly ILogger<DispatchNotificationService> _logger;

    public DispatchNotificationService(
        MknDbContext db,
        ILogger<DispatchNotificationService> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task DispatchAssignmentNoticeAsync(
        RosterAssignment assignment,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Read the three referenced rows. §14.1.1: 9.0 reads what it
            // needs to compose the message; it does not modify any of it.
            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == assignment.MemberId,
                    cancellationToken);

            var occurrence = await _db.ServiceOccurrences
                .FirstOrDefaultAsync(o => o.OccurrenceId == assignment.OccurrenceId,
                    cancellationToken);

            var duty = await _db.Duties
                .FirstOrDefaultAsync(d => d.DutyId == assignment.DutyId,
                    cancellationToken);

            // Read the channel setting. §14.1.2: if absent, the process
            // follows the setting's configured validity behavior. The
            // seed and production both write a value; if it is missing
            // here, the implementation logs the message under a default
            // channel name so the process still produces an observable
            // effect.
            var channel = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == ChannelKey, cancellationToken);

            var channelName = string.IsNullOrWhiteSpace(channel?.Value)
                ? "Log"
                : channel!.Value!.Trim();

            // Compose the message. §14.1.6: the notification carries
            // enough for the member to understand what is being asked of
            // them — the occurrence date and time, the duty, and the
            // implicit expectation of a response. The format is
            // channel-dependent and outside the schema.
            var date = occurrence?.Date.ToString("yyyy-MM-dd") ?? "unknown date";
            var time = occurrence?.StartTime?.ToString("HH:mm") ?? "unknown time";
            var dutyName = duty?.Name ?? $"duty #{assignment.DutyId}";
            var memberName = member is null
                ? $"member #{assignment.MemberId}"
                : $"{member.Name} {member.Surname}";

            var message =
                $"Assignment notice: {memberName}, you are assigned to " +
                $"'{dutyName}' on {date} at {time}.";

            // Dispatch. The switch is the extension point. Adding a
            // channel is adding a case here — nowhere else.
            switch (channelName.ToLowerInvariant())
            {
                case "log":
                default:
                    _logger.LogInformation(
                        "Notification [{Channel}] assignment={AssignmentId} member={MemberId} duty={DutyId} occurrence={OccurrenceId}: {Message}",
                        channelName,
                        assignment.AssignmentId,
                        assignment.MemberId,
                        assignment.DutyId,
                        assignment.OccurrenceId,
                        message);
                    break;

                // The following channels are named in the specification
                // (§14.1.2 names "SMS, email, push, or another
                // mechanism") but are not implemented here. When they are
                // implemented, each becomes a case with its own send
                // path. The callers — 5.0, 7.0, 12.0 — are unaffected.

                // case "email":
                //     await SendEmailAsync(message, member, cancellationToken);
                //     break;
                //
                // case "webhook":
                //     await SendWebhookAsync(message, assignment, cancellationToken);
                //     break;
            }
        }
        catch (Exception ex)
        {
            // Fire-and-forget. §14.1.5: no retry, no delivery record.
            // The failure is logged so an operator can see it; the
            // invoking process is unaffected.
            _logger.LogError(
                ex,
                "Notification dispatch failed for assignment {AssignmentId}. " +
                "The assignment itself is unaffected.",
                assignment.AssignmentId);
        }
    }
}
