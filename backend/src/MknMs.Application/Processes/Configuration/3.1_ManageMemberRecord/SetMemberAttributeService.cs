namespace MknMs.Application.Processes.Configuration.ManageMemberRecord;

/// <summary>
/// Set Member Attribute — the writer for MemberAttributeValue.
/// </summary>
/// <remarks>
/// §10.1.5 makes the MemberAttribute Duty Rule criterion depend on
/// MemberAttributeValue rows; without a writer for those rows the
/// criterion has no reachable candidate set. This service is the
/// write path the criterion needs.
///
/// The (MemberId, AttributeTypeId) pair is unique per §4. Re-setting
/// an existing pair replaces Value, RecordedDate, and RecordedBy on
/// the existing row; the MemberAttributeValueId is preserved. The
/// contract's Conflict error text is honoured as replace, not as
/// refusal — see SetMemberAttributeCommand for the derivation.
///
/// Reads D4 (Member, AttributeType, Admin). Writes D4
/// (MemberAttributeValue only). No other store is touched.
///
/// Specification: §4 (Member Attribute Value), §9.1.7, §10.1.5;
/// docs/processes/contracts.md "Set Member Attribute command".
/// </remarks>
public interface ISetMemberAttributeService
{
    Task<ConfigurationResult<MemberAttributeValue>> SetAsync(
        SetMemberAttributeCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class SetMemberAttributeService : ISetMemberAttributeService
{
    private readonly MknDbContext _db;

    public SetMemberAttributeService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<ConfigurationResult<MemberAttributeValue>> SetAsync(
        SetMemberAttributeCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.MemberId <= 0)
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                "MemberId must be positive.");
        }

        if (command.AttributeTypeId <= 0)
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                "AttributeTypeId must be positive.");
        }

        if (command.ActingAdminId <= 0)
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                "ActingAdminId must be positive.");
        }

        if (!ConfigurationValidation.IsValidName(command.Value))
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                "Value is required.");
        }

        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, command.ActingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<MemberAttributeValue>.Failure(adminError);
        }

        var memberExists = await _db.Members
            .AnyAsync(m => m.MemberId == command.MemberId, cancellationToken);
        if (!memberExists)
        {
            return Failure(
                ConfigurationErrorCodes.NotFound,
                $"Member {command.MemberId} does not exist.");
        }

        var attributeTypeExists = await _db.AttributeTypes
            .AnyAsync(a => a.AttributeTypeId == command.AttributeTypeId
                && !a.IsDeleted,
                cancellationToken);
        if (!attributeTypeExists)
        {
            return Failure(
                ConfigurationErrorCodes.ReferencedEntityInactive,
                $"AttributeType {command.AttributeTypeId} does not exist "
                + "or is soft-deleted.");
        }

        var entity = await _db.MemberAttributeValues
            .FirstOrDefaultAsync(
                v => v.MemberId == command.MemberId
                    && v.AttributeTypeId == command.AttributeTypeId,
                cancellationToken);

        if (entity is null)
        {
            entity = new MemberAttributeValue
            {
                MemberId = command.MemberId,
                AttributeTypeId = command.AttributeTypeId,
                Value = command.Value.Trim(),
                RecordedDate = command.RecordedDate,
                RecordedBy = command.ActingAdminId,
            };
            _db.MemberAttributeValues.Add(entity);
        }
        else
        {
            entity.Value = command.Value.Trim();
            entity.RecordedDate = command.RecordedDate;
            entity.RecordedBy = command.ActingAdminId;
        }

        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<MemberAttributeValue>.Success(entity);
    }

    private static ConfigurationResult<MemberAttributeValue> Failure(
        string code, string message) =>
        ConfigurationResult<MemberAttributeValue>.Failure(
            new ConfigurationError(code, message));
}
