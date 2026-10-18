
namespace MknMs.Application.Processes.Configuration.ManageMemberStatus;

/// <summary>
/// Subprocess 3.4 — Manage Member Status.
/// </summary>
/// <remarks>
/// Only the soft-delete operation is implemented. §3.0 does not
/// name Member Status CRUD, and E4d's scope is the soft-delete
/// cascade from §9.7. List and get are deliberately absent — they
/// land when a consumer requires them.
///
/// The MemberStatus cascade (member fallback to the default
/// status, physical removal of AttendanceRule rows whose
/// OutcomeStatusId names this status, physical removal of
/// AttendanceRuleScope rows whose MemberStatus criterion names
/// this status) lives in ConfigurationCascadeService. This
/// service coordinates: validate, apply the cascade, mark the
/// parent, add the audit row, one SaveChangesAsync.
///
/// Specification: §4 (Member Status), §9.7, §15.8.1.
/// </remarks>
public interface IManageMemberStatusService
{
    Task<ConfigurationResult<MemberStatus>> SoftDeleteAsync(
        int memberStatusId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageMemberStatusService : IManageMemberStatusService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageMemberStatusService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<ConfigurationResult<MemberStatus>> SoftDeleteAsync(
        int memberStatusId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<MemberStatus>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.MemberStatuses
            .FirstOrDefaultAsync(s => s.MemberStatusId == memberStatusId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(MemberStatus), memberStatusId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<MemberStatus>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Member Status is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<MemberStatus>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(MemberStatus), memberStatusId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<MemberStatus>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<MemberStatus>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(MemberStatus),
            EntityId = entity.MemberStatusId,
            EntityName = entity.Name,
            Action = ConfigurationLifecycleActions.SoftDelete,
            Reason = reason,
            ConsequencesText = cascade.Value.ConsequencesText,
            InitiatedByAdminId = actingAdminId,
            ApprovedByAdminId = approvedByAdminId,
            ConsequencesAcknowledged = consequencesAcknowledged,
        }, cancellationToken);

        if (!audit.IsSuccess)
        {
            return ConfigurationResult<MemberStatus>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<MemberStatus>.Success(entity);
    }
}
