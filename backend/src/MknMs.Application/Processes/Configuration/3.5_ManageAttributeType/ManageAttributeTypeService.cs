
namespace MknMs.Application.Processes.Configuration.ManageAttributeType;

/// <summary>
/// Subprocess 3.5 — Manage Attribute Type.
/// </summary>
/// <remarks>
/// Only the soft-delete operation is implemented. §3.0 does not name
/// Attribute Type CRUD, and the E4i scope is the soft-delete cascade
/// from §9.7.
///
/// The Attribute Type cascade (removal of MemberAttributeValue rows
/// for this attribute, soft delete of Duty Rules whose
/// MemberAttribute criterion names this attribute) lives in
/// ConfigurationCascadeService. This service coordinates:
/// validate, apply the cascade, mark the parent, add the audit row,
/// one SaveChangesAsync.
///
/// Specification: §4 (Attribute Type), §9.7, §15.8.4.
/// </remarks>
public interface IManageAttributeTypeService
{
    Task<IReadOnlyList<AttributeType>> ListAsync(
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttributeType>> CreateAsync(
        string name,
        string? description,
        int actingAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<AttributeType>> SoftDeleteAsync(
        int attributeTypeId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageAttributeTypeService : IManageAttributeTypeService
{
    private readonly MknDbContext _db;
    private readonly IConfigurationLifecycleService _lifecycle;
    private readonly IConfigurationCascadeService _cascade;

    public ManageAttributeTypeService(
        MknDbContext db,
        IConfigurationLifecycleService lifecycle,
        IConfigurationCascadeService cascade)
    {
        _db = db;
        _lifecycle = lifecycle;
        _cascade = cascade;
    }

    public async Task<IReadOnlyList<AttributeType>> ListAsync(
        CancellationToken cancellationToken = default)
    {
        return await _db.AttributeTypes
            .Where(a => !a.IsDeleted)
            .OrderBy(a => a.AttributeTypeId)
            .ToListAsync(cancellationToken);
    }

    public async Task<ConfigurationResult<AttributeType>> CreateAsync(
        string name,
        string? description,
        int actingAdminId,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return ConfigurationResult<AttributeType>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue, "Name is required."));
        }

        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, actingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<AttributeType>.Failure(adminError);
        }

        var entity = new AttributeType
        {
            Name = name.Trim(),
            Description = string.IsNullOrWhiteSpace(description)
                ? null
                : description.Trim(),
            IsActive = true,
            IsDeleted = false,
        };

        _db.AttributeTypes.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<AttributeType>.Success(entity);
    }

    public async Task<ConfigurationResult<AttributeType>> SoftDeleteAsync(
        int attributeTypeId,
        int actingAdminId,
        int approvedByAdminId,
        bool consequencesAcknowledged,
        string reason,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(reason))
        {
            return ConfigurationResult<AttributeType>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.InvalidValue,
                    "Reason is required for soft delete."));
        }

        var entity = await _db.AttributeTypes
            .FirstOrDefaultAsync(a => a.AttributeTypeId == attributeTypeId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(AttributeType), attributeTypeId);
        }

        if (entity.IsDeleted)
        {
            return ConfigurationResult<AttributeType>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.AlreadyInactive,
                    "Attribute Type is already soft-deleted."));
        }

        if (!consequencesAcknowledged)
        {
            return ConfigurationResult<AttributeType>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.ValidationFailed,
                    "The consequences of this soft delete must be acknowledged."));
        }

        var cascade = await _cascade.ApplyAsync(
            nameof(AttributeType), attributeTypeId, cancellationToken);
        if (!cascade.IsSuccess)
        {
            return ConfigurationResult<AttributeType>.Failure(cascade.Error!);
        }

        if (cascade.Value!.IsBlocked)
        {
            return ConfigurationResult<AttributeType>.Failure(
                new ConfigurationError(
                    ConfigurationErrorCodes.LifecycleViolation,
                    cascade.Value.BlockedReason ?? "Soft delete is blocked."));
        }

        entity.IsDeleted = true;
        entity.IsActive = false;

        var audit = await _lifecycle.RecordAsync(new ConfigurationLifecycleEvent
        {
            EntityType = nameof(AttributeType),
            EntityId = entity.AttributeTypeId,
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
            return ConfigurationResult<AttributeType>.Failure(audit.Error!);
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<AttributeType>.Success(entity);
    }
}
