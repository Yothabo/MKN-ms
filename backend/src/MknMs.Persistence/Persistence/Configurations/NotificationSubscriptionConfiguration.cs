
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace MknMs.Persistence.Configurations;

/// <summary>
/// EF Core mapping for NotificationSubscription.
/// </summary>
/// <remarks>
/// Exactly one of RecipientTierId or RecipientAdminId is set per row.
/// Enforced by a check constraint.
///
/// Specification: §4 (Notification Subscription), §14.1.3, §15.8.16.
/// </remarks>
public class NotificationSubscriptionConfiguration : IEntityTypeConfiguration<NotificationSubscription>
{
    public void Configure(EntityTypeBuilder<NotificationSubscription> builder)
    {
        builder.HasKey(e => e.SubscriptionId);

        builder.Property(e => e.EventType)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(e => e.RecipientTierId);

        builder.Property(e => e.RecipientAdminId);

        builder.Property(e => e.IsActive)
            .IsRequired();

        builder.Property(e => e.IsDeleted)
            .IsRequired();

        builder.HasOne(e => e.RecipientTier)
            .WithMany()
            .HasForeignKey(e => e.RecipientTierId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.HasOne(e => e.RecipientAdmin)
            .WithMany()
            .HasForeignKey(e => e.RecipientAdminId)
            .OnDelete(DeleteBehavior.Restrict)
            .IsRequired(false);

        builder.ToTable(t => t.HasCheckConstraint(
            "chk_notification_subscription_recipient",
            "(recipient_tier_id IS NOT NULL AND recipient_admin_id IS NULL) OR " +
            "(recipient_tier_id IS NULL AND recipient_admin_id IS NOT NULL)"));
    }
}
