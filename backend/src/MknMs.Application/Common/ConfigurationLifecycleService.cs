namespace MknMs.Application.Common;

/// <summary>
/// Records configuration lifecycle events — deactivations and soft
/// deletes — as ConfigurationAuditLog rows, after enforcing the
/// EntityDeletionPolicy requirements that apply to the operation.
/// </summary>
/// <remarks>
/// §9.7: every deactivation and every soft delete presents a
/// consequences preview, requires acknowledgement, requires approval
/// from an admin of a configured permission tier, and writes a
/// ConfigurationAuditLog row. The requirement and the approval tier
/// are configured per entity type in EntityDeletionPolicy. The
/// reason is required for soft delete and optional for deactivate by
/// default; EntityDeletionPolicy overrides both.
///
/// This service enforces the policy: approval requirement, approver
/// permission tier, and reason requirement. It composes the
/// consequences preview internally from the operation kind and the
/// entity's identity. It adds the audit row to the DbContext but does
/// not call SaveChangesAsync — the caller controls the persistence
/// boundary so the state change and the audit row are one atomic
/// unit (§9.7, §9.8).
///
/// A missing EntityDeletionPolicy row is a configuration error. The
/// operation refuses with ConfigurationMissing. §9.7 requires the
/// policy to govern the lifecycle operation; a policy that is absent
/// cannot silently mean "no approval, no tier, no reason", because
/// that would bypass the requirement rather than satisfy it.
///
/// Acknowledgement of the preview is enforced by the calling
/// command's ConsequencesAcknowledged field, introduced in E3b. This
/// service generates the preview; it does not check that the caller
/// has seen it.
///
/// Specification: §4 (Configuration Audit Log, Entity Deletion
/// Policy), §9.7, §9.8, §15.8.14, §15.8.15.
/// </remarks>
public interface IConfigurationLifecycleService
{
    Task<ConfigurationResult<ConfigurationAuditLog>> RecordAsync(
        ConfigurationLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default);
}

public static class ConfigurationLifecycleActions
{
    public const string Deactivate = "Deactivate";
    public const string SoftDelete = "SoftDelete";
}

/// <summary>
/// Inputs to one lifecycle event.
/// </summary>
/// <remarks>
/// The service composes the consequences preview and the
/// consequences that occurred. The caller supplies only the facts:
/// what entity, what operation, who initiated, who approved, and the
/// optional reason.
/// </remarks>
public sealed record ConfigurationLifecycleEvent
{
    public required string EntityType { get; init; }

    public required int EntityId { get; init; }

    public required string EntityName { get; init; }

    public required string Action { get; init; }

    public string? Reason { get; init; }

    /// <summary>
    /// The cascade's description of the dependent changes the soft
    /// delete is about to apply. Set by the caller for a soft delete
    /// after ConfigurationCascadeService.ApplyAsync has prepared the
    /// dependent changes. Null for a deactivate, or for a soft delete
    /// whose entity has no cascade.
    /// </summary>
    public string? ConsequencesText { get; init; }

    public required int InitiatedByAdminId { get; init; }

    public required int ApprovedByAdminId { get; init; }
}

public sealed class ConfigurationLifecycleService : IConfigurationLifecycleService
{
    private readonly MknDbContext _db;
    private readonly TimeProvider _clock;

    public ConfigurationLifecycleService(MknDbContext db, TimeProvider clock)
    {
        _db = db;
        _clock = clock;
    }

    public async Task<ConfigurationResult<ConfigurationAuditLog>> RecordAsync(
        ConfigurationLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default)
    {
        var basicValidation = ValidateBasics(lifecycleEvent);
        if (basicValidation is not null)
        {
            return ConfigurationResult<ConfigurationAuditLog>.Failure(basicValidation);
        }

        // Resolve the applicable policy row. A missing policy is a
        // configuration error, not a silent bypass. §9.7 requires the
        // policy to govern the operation; if the policy that governs
        // this entity type is not configured, the operation cannot
        // proceed.
        var policy = await _db.EntityDeletionPolicies
            .FirstOrDefaultAsync(p => p.EntityType == lifecycleEvent.EntityType
                && p.IsActive
                && !p.IsDeleted,
                cancellationToken);

        if (policy is null)
        {
            return Failure(
                ConfigurationErrorCodes.ConfigurationMissing,
                $"No EntityDeletionPolicy is configured for entity type "
                + $"'{lifecycleEvent.EntityType}'. The {lifecycleEvent.Action} "
                + "cannot proceed without a policy.");
        }

        var requiresApproval = lifecycleEvent.Action == ConfigurationLifecycleActions.SoftDelete
            ? policy.RequiresApprovalForDelete
            : policy.RequiresApprovalForDeactivate;

        var requiresReason = lifecycleEvent.Action == ConfigurationLifecycleActions.SoftDelete
            ? policy.RequiresReasonForDelete
            : policy.RequiresReasonForDeactivate;

        var requiredTierId = lifecycleEvent.Action == ConfigurationLifecycleActions.SoftDelete
            ? policy.RequiredDeletePermissionTierId
            : policy.RequiredDeactivatePermissionTierId;

        // Reason requirement.
        if (requiresReason && string.IsNullOrWhiteSpace(lifecycleEvent.Reason))
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                $"Reason is required for {lifecycleEvent.Action} of "
                + $"{lifecycleEvent.EntityType}.");
        }

        // Approval requirement.
        if (requiresApproval
            && lifecycleEvent.ApprovedByAdminId == lifecycleEvent.InitiatedByAdminId)
        {
            return Failure(
                ConfigurationErrorCodes.LifecycleViolation,
                $"Approval is required for {lifecycleEvent.Action} of "
                + $"{lifecycleEvent.EntityType}; a separate approver must "
                + "be supplied.");
        }

        // Approver resolution. The approver's persisted Admin row is
        // always read; the service does not trust an id supplied by
        // the caller. C6: the acting admin must resolve to a row
        // whose IsActive = true.
        var approver = await _db.Admins
            .FirstOrDefaultAsync(a => a.AdminId == lifecycleEvent.ApprovedByAdminId,
                cancellationToken);

        if (approver is null)
        {
            return Failure(
                ConfigurationErrorCodes.ReferencedEntityMissing,
                $"Approver {lifecycleEvent.ApprovedByAdminId} does not exist.");
        }

        if (!approver.IsActive)
        {
            return Failure(
                ConfigurationErrorCodes.ReferencedEntityInactive,
                $"Approver {lifecycleEvent.ApprovedByAdminId} is not active.");
        }

        // Approver tier check. Only runs when the entity's policy
        // configures a required tier for this operation.
        if (requiredTierId is int tierId)
        {
            if (approver.PermissionTierId != tierId)
            {
                return Failure(
                    ConfigurationErrorCodes.LifecycleViolation,
                    $"Approver {lifecycleEvent.ApprovedByAdminId} does not hold "
                    + $"the configured approval permission tier {tierId}.");
            }
        }

        // Compose the preview from the operation kind and the entity's
        // identity. The full cascade rules (E4) are not implemented
        // yet, so the preview describes what this operation actually
        // does at this phase.
        var preview = ComposePreview(lifecycleEvent);

        var now = _clock.GetUtcNow();

        var audit = new ConfigurationAuditLog
        {
            EntityType = lifecycleEvent.EntityType,
            EntityId = lifecycleEvent.EntityId,
            EntityName = lifecycleEvent.EntityName,
            Action = lifecycleEvent.Action,
            Reason = string.IsNullOrWhiteSpace(lifecycleEvent.Reason)
                ? null
                : lifecycleEvent.Reason.Trim(),
            InitiatedByAdminId = lifecycleEvent.InitiatedByAdminId,
            ApprovedByAdminId = lifecycleEvent.ApprovedByAdminId,
            InitiatedAt = now,
            ApprovedAt = now,
            ConsequencesPreviewed = preview,
            ConsequencesOccurred = preview,
            NotifiedAt = null,
        };

        _db.ConfigurationAuditLogs.Add(audit);

        return ConfigurationResult<ConfigurationAuditLog>.Success(audit);
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static string ComposePreview(ConfigurationLifecycleEvent e)
    {
        var verb = e.Action == ConfigurationLifecycleActions.SoftDelete
            ? "soft-deleted"
            : "deactivated";

        // For a soft delete whose cascade has already run, the
        // cascade's own description of what it prepared is the
        // faithful preview. For a deactivate, or a soft delete with
        // no cascade, the generic statement is used.
        var consequence = !string.IsNullOrWhiteSpace(e.ConsequencesText)
            ? e.ConsequencesText!
            : e.Action == ConfigurationLifecycleActions.SoftDelete
                ? "No dependent changes apply."
                : "Existing references are unaffected.";

        return $"{e.EntityType} '{e.EntityName}' (id {e.EntityId}) will be "
            + $"{verb}. {consequence}";
    }

    private static ConfigurationError? ValidateBasics(ConfigurationLifecycleEvent e)
    {
        if (!ConfigurationValidation.IsValidName(e.EntityType))
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue, "EntityType is required.");
        }

        if (e.EntityId <= 0)
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue, "EntityId must be positive.");
        }

        if (!ConfigurationValidation.IsValidName(e.EntityName))
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue, "EntityName is required.");
        }

        if (e.Action != ConfigurationLifecycleActions.Deactivate
            && e.Action != ConfigurationLifecycleActions.SoftDelete)
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                $"Action must be '{ConfigurationLifecycleActions.Deactivate}' "
                + $"or '{ConfigurationLifecycleActions.SoftDelete}'.");
        }

        if (e.InitiatedByAdminId <= 0)
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                "InitiatedByAdminId must be positive.");
        }

        if (e.ApprovedByAdminId <= 0)
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                "ApprovedByAdminId must be positive.");
        }

        return null;
    }

    private static ConfigurationResult<ConfigurationAuditLog> Failure(
        string code, string message) =>
        ConfigurationResult<ConfigurationAuditLog>.Failure(
            new ConfigurationError(code, message));
}
