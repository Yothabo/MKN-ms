namespace MknMs.Application.Common;

/// <summary>
/// Records configuration lifecycle events — deactivations and soft
/// deletes — as ConfigurationAuditLog rows.
/// </summary>
/// <remarks>
/// §9.7: every deactivation and every soft delete writes a
/// ConfigurationAuditLog row carrying the entity type, the affected
/// row's id and name at the time of the action, the action performed,
/// the reason when one is supplied, who initiated it and who
/// approved it, and the timestamps.
///
/// This service adds the audit row to the DbContext. It does not
/// call SaveChangesAsync; the calling service controls the
/// persistence boundary so the state change and the audit row are
/// one atomic unit (§9.7, §9.8). It does not read
/// EntityDeletionPolicy; that is E3. It does not perform the
/// cascade from §9.7's table; that is E4.
///
/// The action vocabulary is the two values §4 names:
///
///   - Deactivate — IsActive set false, IsDeleted unchanged.
///   - SoftDelete — IsDeleted set true, implicitly IsActive false.
///
/// ConsequencesPreviewed and ConsequencesOccurred are required by the
/// entity. §9.7 states the operation presents a preview and records
/// what actually happened; the exact serialisation is an
/// implementation choice and is passed in by the caller so the audit
/// row is a faithful record of what the operation showed and what it
/// did.
///
/// Specification: §4 (Configuration Audit Log), §9.7, §15.8.14.
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
/// Inputs to one audit write.
/// </summary>
public sealed record ConfigurationLifecycleEvent
{
    public required string EntityType { get; init; }

    public required int EntityId { get; init; }

    public required string EntityName { get; init; }

    public required string Action { get; init; }

    public string? Reason { get; init; }

    public required int InitiatedByAdminId { get; init; }

    public required int ApprovedByAdminId { get; init; }

    public required string ConsequencesPreviewed { get; init; }

    public required string ConsequencesOccurred { get; init; }
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

    public Task<ConfigurationResult<ConfigurationAuditLog>> RecordAsync(
        ConfigurationLifecycleEvent lifecycleEvent,
        CancellationToken cancellationToken = default)
    {
        var validation = Validate(lifecycleEvent);
        if (validation is not null)
        {
            return Task.FromResult(
                ConfigurationResult<ConfigurationAuditLog>.Failure(validation));
        }

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
            ConsequencesPreviewed = lifecycleEvent.ConsequencesPreviewed,
            ConsequencesOccurred = lifecycleEvent.ConsequencesOccurred,
            NotifiedAt = null,
        };

        // The audit entity is added to the DbContext but not yet
        // persisted. The caller performs the single SaveChangesAsync
        // that commits both the state change and this audit row in
        // one transaction (§9.7, §9.8).
        _db.ConfigurationAuditLogs.Add(audit);

        return Task.FromResult(
            ConfigurationResult<ConfigurationAuditLog>.Success(audit));
    }

    private static ConfigurationError? Validate(ConfigurationLifecycleEvent e)
    {
        if (!ConfigurationValidation.IsValidName(e.EntityType))
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                "EntityType is required.");
        }

        if (e.EntityId <= 0)
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                "EntityId must be positive.");
        }

        if (!ConfigurationValidation.IsValidName(e.EntityName))
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                "EntityName is required.");
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

        if (!ConfigurationValidation.IsValidName(e.ConsequencesPreviewed))
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                "ConsequencesPreviewed is required.");
        }

        if (!ConfigurationValidation.IsValidName(e.ConsequencesOccurred))
        {
            return new ConfigurationError(
                ConfigurationErrorCodes.InvalidValue,
                "ConsequencesOccurred is required.");
        }

        return null;
    }
}
