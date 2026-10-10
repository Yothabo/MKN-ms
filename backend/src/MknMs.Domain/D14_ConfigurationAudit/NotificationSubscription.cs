
namespace MknMs.Domain.D14_ConfigurationAudit;

/// <summary>
/// Recipient configuration for authority notifications.
/// </summary>
/// <remarks>
/// Exactly one of RecipientTierId or RecipientAdminId is set per row.
/// When the tier is set, all admins of that tier receive the
/// notification. When the admin is set, only that admin receives it.
///
/// Specification: §4 (Notification Subscription), §14.1.3, §15.8.16.
/// </remarks>
public class NotificationSubscription
{
    public int SubscriptionId { get; set; }

    public string EventType { get; set; } = null!;

    public int? RecipientTierId { get; set; }

    public int? RecipientAdminId { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsDeleted { get; set; }

    // Navigation
    public PermissionTier? RecipientTier { get; set; }

    public Admin? RecipientAdmin { get; set; }
}
