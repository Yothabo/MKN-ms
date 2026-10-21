namespace MknMs.Application.Processes.Configuration.ManageNotificationSubscription;

/// <summary>
/// Manage Notification Subscription — recipient configuration for
/// the 9.0 Path 2 authority-notification sweep.
/// </summary>
/// <remarks>
/// A NotificationSubscription names either a PermissionTier or a
/// specific Admin. When the tier is named, every active admin of
/// that tier receives the notification. When an admin is named, only
/// that admin receives it. Exactly one of the two recipient fields
/// is set; the DB check constraint chk_notification_subscription_recipient
/// enforces the rule, and this service rejects first with a clearer
/// message.
///
/// EventType must be one of the two strings the sweep matches
/// against: "Deactivate" or "SoftDelete". The sweep resolves
/// recipients with s.EventType == audit.Action, and audit.Action is
/// ConfigurationLifecycleActions.Deactivate or .SoftDelete. A
/// subscription with an unrecognised EventType would be silently
/// ignored by the sweep; this service refuses the write instead.
///
/// Two lifecycle operations, both routed through
/// IConfigurationLifecycleService per §9.7:
///   - DeactivateAsync sets IsActive = false. Reversible.
///   - SoftDeleteAsync sets IsDeleted = true and IsActive = false.
///     The cascade arm returns NoDependents() because nothing
///     references a subscription.
///
/// Specification: §4 (Notification Subscription), §9.7, §14.1.3,
/// §14.1.9, §15.8.16.
/// </remarks>
public interface IManageNotificationSubscriptionService
{
    Task<IReadOnlyList<NotificationSubscription>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<NotificationSubscription?> GetAsync(
        int subscriptionId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<NotificationSubscription>> CreateAsync(
        string eventType,
        int? recipientTierId,
        int? recipientAdminId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<NotificationSubscription>> ReactivateAsync(
        int subscriptionId,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<NotificationSubscription>> DeactivateAsync(
        int subscriptionId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<NotificationSubscription>> SoftDeleteAsync(
        int subscriptionId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageNotificationSubscriptionService : IManageNotificationSubscriptionService
{
    private static readonly HashSet<string> ValidEventTypes = new(StringComparer.Ordinal)
    {
        "Deactivate",
        "SoftDelete",
    };

    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageNotificationSubscriptionService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<IReadOnlyList<NotificationSubscription>> ListAsync(
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.NotificationSubscriptions
            .Where(s => !s.IsDeleted);

        if (!includeInactive)
        {
            query = query.Where(s => s.IsActive);
        }

        return await query
            .OrderBy(s => s.SubscriptionId)
            .ToListAsync(cancellationToken);
    }

    public async Task<NotificationSubscription?> GetAsync(
        int subscriptionId,
        CancellationToken cancellationToken = default)
    {
        return await _db.NotificationSubscriptions
            .FirstOrDefaultAsync(
                s => s.SubscriptionId == subscriptionId && !s.IsDeleted,
                cancellationToken);
    }

    public async Task<ConfigurationResult<NotificationSubscription>> CreateAsync(
        string eventType,
        int? recipientTierId,
        int? recipientAdminId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        if (!ValidEventTypes.Contains(eventType))
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    $"EventType '{eventType}' is not one of: Deactivate, SoftDelete."));
        }

        var tierSet = recipientTierId is int;
        var adminSet = recipientAdminId is int;

        if (tierSet == adminSet)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Exactly one of RecipientTierId or RecipientAdminId must be set."));
        }

        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(adminError);
        }

        if (tierSet)
        {
            var tierId = recipientTierId!.Value;
            var tierExists = await _db.PermissionTiers
                .AnyAsync(t => t.PermissionTierId == tierId
                    && t.IsActive && !t.IsDeleted,
                    cancellationToken);
            if (!tierExists)
            {
                return ConfigurationResult<NotificationSubscription>.Failure(
                    new ConfigurationError(
                        ConfigurationErrorCodes.ReferencedEntityInactive,
                        $"PermissionTier {tierId} does not exist, is inactive, "
                        + "or is soft-deleted."));
            }
        }
        else
        {
            var adminId = recipientAdminId!.Value;
            var adminActive = await _db.Admins
                .AnyAsync(a => a.AdminId == adminId && a.IsActive,
                    cancellationToken);
            if (!adminActive)
            {
                return ConfigurationResult<NotificationSubscription>.Failure(
                    new ConfigurationError(
                        ConfigurationErrorCodes.ReferencedEntityInactive,
                        $"Admin {adminId} does not exist or is not active."));
            }
        }

        var entity = new NotificationSubscription
        {
            EventType = eventType,
            RecipientTierId = recipientTierId,
            RecipientAdminId = recipientAdminId,
            IsActive = true,
            IsDeleted = false,
        };

        _db.NotificationSubscriptions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<NotificationSubscription>.Success(entity);
    }

    public async Task<ConfigurationResult<NotificationSubscription>> ReactivateAsync(
        int subscriptionId,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(adminError);
        }

        var entity = await _db.NotificationSubscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(NotificationSubscription), subscriptionId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    "A soft-deleted NotificationSubscription cannot be reactivated."));
        }

        if (entity.IsActive)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyActive,
                    "NotificationSubscription is already active."));
        }

        // §9.7 scopes the audit-write requirement to deactivation and
        // soft delete. Reactivation writes no audit row.
        entity.IsActive = true;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<NotificationSubscription>.Success(entity);
    }

    public async Task<ConfigurationResult<NotificationSubscription>> DeactivateAsync(
        int subscriptionId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string? reason = null,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.NotificationSubscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId,
                cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(
                nameof(NotificationSubscription), subscriptionId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Notification subscription is already inactive."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this deactivation must be acknowledged."));
        }

        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(NotificationSubscription),
            EntityId = entity.SubscriptionId,
            EntityName = $"Subscription {entity.SubscriptionId} ({entity.EventType})",
            Action = ConfigurationLifecycleActions.Deactivate,
            Reason = reason,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<NotificationSubscription>.Success(entity);
    }

    public async Task<ConfigurationResult<NotificationSubscription>> SoftDeleteAsync(
        int subscriptionId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.NotificationSubscriptions
            .FirstOrDefaultAsync(s => s.SubscriptionId == subscriptionId,
                cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(
                nameof(NotificationSubscription), subscriptionId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Notification subscription is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(NotificationSubscription), subscriptionId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(NotificationSubscription),
            EntityId = entity.SubscriptionId,
            EntityName = $"Subscription {entity.SubscriptionId} ({entity.EventType})",
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<NotificationSubscription>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<NotificationSubscription>.Success(entity);
    }
}
