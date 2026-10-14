
using Microsoft.Extensions.Logging;

namespace MknMs.Application.Processes.Operations.DispatchNotification;

/// <summary>
/// Implementation of the authority-notification sweep (9.0 Path 2).
/// </summary>
/// <remarks>
/// Reads every ConfigurationAuditLog row whose NotifiedAt is null,
/// resolves its recipients through the active subscriptions that
/// match its EventType, dispatches one message per recipient through
/// the channel named by SystemSetting.NotificationChannel, and sets
/// NotifiedAt when the whole row's dispatch succeeds.
///
/// The audit row is the retry unit. A per-recipient dispatch failure
/// leaves NotifiedAt null; the next sweep retries the row. This can
/// produce duplicate dispatches to recipients who were notified
/// before the failure — that is §14.1.4's at-least-once semantics.
///
/// Recipient resolution, §14.1.9:
///   - Inactive or soft-deleted subscription: not consulted.
///   - By tier: every Admin at that tier whose IsActive = true.
///   - By specific admin: that admin if Admin.IsActive = true.
///   - Zero recipients: valid. NotifiedAt is set.
///
/// The channel setting is required for the sweep to run; when it is
/// absent or unrecognised the sweep refuses and returns Failure
/// without processing pending rows.
///
/// Specification: §14.1.3, §14.1.4, §14.1.9, §16.4.
/// </remarks>
public sealed class AuthorityNotificationSweepService : IAuthorityNotificationSweepService
{
    private const string ChannelKey = "NotificationChannel";
    private const string LogChannel = "log";

    private readonly MknDbContext _db;
    private readonly TimeProvider _clock;
    private readonly ILogger<AuthorityNotificationSweepService> _logger;

    public AuthorityNotificationSweepService(
        MknDbContext db,
        TimeProvider clock,
        ILogger<AuthorityNotificationSweepService> logger)
    {
        _db = db;
        _clock = clock;
        _logger = logger;
    }

    public async Task<AuthorityNotificationSweepResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        // Resolve the channel. Required for the sweep. Absent or
        // unrecognised → refuse, no processing.
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == ChannelKey, cancellationToken);

        if (string.IsNullOrWhiteSpace(setting?.Value))
        {
            return Failure(
                ConfigurationErrorCodes.ConfigurationMissing,
                $"Required setting '{ChannelKey}' is not configured.");
        }

        var channelName = setting!.Value!.Trim();

        if (!string.Equals(channelName, LogChannel, StringComparison.OrdinalIgnoreCase))
        {
            return Failure(
                ConfigurationErrorCodes.ConfigurationInvalid,
                $"NotificationChannel '{channelName}' is not a recognised channel.");
        }

        // Pending audit rows.
        var pending = await _db.ConfigurationAuditLogs
            .Where(a => a.NotifiedAt == null)
            .OrderBy(a => a.AuditId)
            .ToListAsync(cancellationToken);

        if (pending.Count == 0)
        {
            return Success(0, 0, 0);
        }

        var processed = 0;
        var dispatched = 0;
        var retry = 0;

        foreach (var audit in pending)
        {
            // The subscription's EventType names the kind of event,
            // matching the audit row's Action (Deactivate or
            // SoftDelete). It does not name the entity type.
            var recipientIds = await ResolveRecipientsAsync(
                audit.Action, cancellationToken);

            var rowFailed = false;

            foreach (var adminId in recipientIds)
            {
                try
                {
                    await DispatchToAdminAsync(audit, adminId, channelName, cancellationToken);
                    dispatched++;
                }
                catch (Exception ex)
                {
                    // Per-recipient failure. The row's NotifiedAt is
                    // left null; the row is retried on a later sweep.
                    rowFailed = true;
                    _logger.LogError(
                        ex,
                        "Authority notification failed for audit {AuditId} to admin {AdminId}. "
                        + "The row will be retried on a later sweep.",
                        audit.AuditId, adminId);
                    break;
                }
            }

            if (rowFailed)
            {
                retry++;
                continue;
            }

            audit.NotifiedAt = _clock.GetUtcNow();
            await _db.SaveChangesAsync(cancellationToken);
            processed++;
        }

        return Success(processed, dispatched, retry);
    }

    // -----------------------------------------------------------------
    // Recipient resolution (§14.1.9)
    // -----------------------------------------------------------------

    private async Task<List<int>> ResolveRecipientsAsync(
        string eventType,
        CancellationToken cancellationToken)
    {
        var subscriptions = await _db.NotificationSubscriptions
            .Where(s => s.EventType == eventType
                && s.IsActive
                && !s.IsDeleted)
            .ToListAsync(cancellationToken);

        var ids = new HashSet<int>();

        foreach (var sub in subscriptions)
        {
            if (sub.RecipientTierId is int tierId)
            {
                var adminsByTier = await _db.Admins
                    .Where(a => a.PermissionTierId == tierId && a.IsActive)
                    .Select(a => a.AdminId)
                    .ToListAsync(cancellationToken);

                foreach (var id in adminsByTier)
                {
                    ids.Add(id);
                }
            }
            else if (sub.RecipientAdminId is int adminId)
            {
                var exists = await _db.Admins
                    .AnyAsync(a => a.AdminId == adminId && a.IsActive, cancellationToken);
                if (exists)
                {
                    ids.Add(adminId);
                }
            }
        }

        return ids.OrderBy(id => id).ToList();
    }

    // -----------------------------------------------------------------
    // Dispatch
    // -----------------------------------------------------------------

    private async Task DispatchToAdminAsync(
        ConfigurationAuditLog audit,
        int adminId,
        string channelName,
        CancellationToken cancellationToken)
    {
        // Resolve the admin's display name for the message.
        var admin = await _db.Admins
            .Where(a => a.AdminId == adminId)
            .Join(_db.Members,
                a => a.MemberId,
                m => m.MemberId,
                (a, m) => new { m.Name, m.Surname })
            .FirstOrDefaultAsync(cancellationToken);

        var adminName = admin is null
            ? $"admin #{adminId}"
            : $"{admin.Name} {admin.Surname}";

        var message =
            $"Configuration notice: '{audit.EntityType}' "
            + $"(id {audit.EntityId}, name '{audit.EntityName}') was "
            + $"{audit.Action.ToLowerInvariant()}"
            + (string.IsNullOrWhiteSpace(audit.Reason)
                ? "."
                : $": {audit.Reason}.");

        // Structured logging is the only transport implemented. The
        // §14.1.2 channel vocabulary names others; each becomes its
        // own case with its own send path, and callers are unaffected.
        _logger.LogInformation(
            "Authority notification [{Channel}] audit={AuditId} admin={AdminId} ({AdminName}): {Message}",
            channelName, audit.AuditId, adminId, adminName, message);
    }

    // -----------------------------------------------------------------
    // Result helpers
    // -----------------------------------------------------------------

    private static AuthorityNotificationSweepResult Success(
        int processed, int dispatched, int retry) =>
        new()
        {
            Status = "Success",
            AuditRowsProcessed = processed,
            NotificationsDispatched = dispatched,
            RowsLeftForRetry = retry,
        };

    private static AuthorityNotificationSweepResult Failure(string code, string detail) =>
        new()
        {
            Status = "Failure",
            AuditRowsProcessed = 0,
            NotificationsDispatched = 0,
            RowsLeftForRetry = 0,
            ErrorCode = code,
            ErrorDetail = detail,
        };
}
