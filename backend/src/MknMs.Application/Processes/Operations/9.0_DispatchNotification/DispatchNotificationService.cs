using Microsoft.Extensions.Logging;

namespace MknMs.Application.Processes.Operations.DispatchNotification;

/// <summary>
/// The real Dispatch Notification process (9.0).
/// </summary>
/// <remarks>
/// Reads the assignment, the member, the occurrence, and the configured
/// channel setting. Composes a message. Dispatches it through the
/// channel named by SystemSetting.NotificationChannel.
///
/// §14.1.2 places the channel mechanism outside the schema: "the actual
/// credentials and transport live outside the schema." Logging is a
/// legitimate transport and is the only one implemented here.
///
/// §14.1.2 also specifies the absent-or-invalid behaviour: no
/// channel-specific fallback is defined by this contract, and the
/// setting's Required flag governs. If NotificationChannel is absent
/// and Required = true, the process refuses to send and surfaces a
/// configuration error. If it is absent and Required = false, no
/// notification is sent. If the value is not a recognised channel, no
/// notification is sent. There is no implicit default channel.
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
    private const string LogChannel = "log";

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
            // Read the channel setting. §14.1.2: no fallback is defined
            // by the contract. A required-and-absent setting surfaces a
            // configuration error; an optional-and-absent or
            // unrecognised setting produces no send.
            var setting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == ChannelKey, cancellationToken);

            if (string.IsNullOrWhiteSpace(setting?.Value))
            {
                if (setting?.Required ?? false)
                {
                    throw new InvalidOperationException(
                        $"Required setting '{ChannelKey}' is not configured. " +
                        "Notification dispatch cannot proceed.");
                }

                // Optional and absent: no notification is sent.
                return;
            }

            var channelName = setting!.Value!.Trim();

            // Only the log channel is implemented. An unrecognised
            // value is not a fallback to logging; nothing is sent.
            if (!string.Equals(channelName, LogChannel, StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

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

            // Compose the message. §14.1.6: the notification carries
            // enough for the member to understand what is being asked of
            // them — the occurrence date and time, the duty, and the
            // implicit expectation of a response.
            var date = occurrence?.Date.ToString("yyyy-MM-dd") ?? "unknown date";
            var time = occurrence?.StartTime?.ToString("HH:mm") ?? "unknown time";
            var dutyName = duty?.Name ?? $"duty #{assignment.DutyId}";
            var memberName = member is null
                ? $"member #{assignment.MemberId}"
                : $"{member.Name} {member.Surname}";

            var message =
                $"Assignment notice: {memberName}, you are assigned to " +
                $"'{dutyName}' on {date} at {time}.";

            _logger.LogInformation(
                "Notification [{Channel}] assignment={AssignmentId} member={MemberId} duty={DutyId} occurrence={OccurrenceId}: {Message}",
                channelName,
                assignment.AssignmentId,
                assignment.MemberId,
                assignment.DutyId,
                assignment.OccurrenceId,
                message);

            // Additional channels — Email, Webhook, SMS — are named in
            // the specification (§14.1.2) and are not implemented here.
            // Each becomes its own case with its own send path. The
            // callers — 5.0, 7.0, 12.0 — are unaffected.
        }
        catch (InvalidOperationException)
        {
            // Required-setting failure. Rethrow so the invoking process
            // surfaces the configuration error, consistent with how
            // other required settings behave elsewhere.
            throw;
        }
        catch (Exception ex)
        {
            // Fire-and-forget. §14.1.5: no retry, no delivery record.
            _logger.LogError(
                ex,
                "Notification dispatch failed for assignment {AssignmentId}. " +
                "The assignment itself is unaffected.",
                assignment.AssignmentId);
        }
    }
}
