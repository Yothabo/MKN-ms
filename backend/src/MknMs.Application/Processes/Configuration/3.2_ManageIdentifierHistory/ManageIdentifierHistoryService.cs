namespace MknMs.Application.Processes.Configuration.ManageIdentifierHistory;

/// <summary>
/// Subprocess 3.2 — Manage Identifier History.
/// </summary>
/// <remarks>
/// Identifier History is a per-member log of identifier assignments.
/// Type is free text. Number is admin-supplied. Entries are added and
/// retired via UnassignedDate; they are never deleted. No IsActive
/// flag — the entry is a historical record.
///
/// The system does not prevent two active identifiers of the same Type
/// for one member. That is a configuration error, not a schema error.
///
/// Specification: §4 (Identifier History), §9.1.7.
/// </remarks>
public interface IManageIdentifierHistoryService
{
    Task<IReadOnlyList<IdentifierHistory>> ListForMemberAsync(
        int memberId,
        CancellationToken cancellationToken = default);

    Task<IdentifierHistory?> GetAsync(int entryId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<IdentifierHistory>> IssueAsync(
        int memberId,
        string type,
        string number,
        DateOnly assignedDate,
        int authorizedByAdminId,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<IdentifierHistory>> RetireAsync(
        int entryId,
        DateOnly unassignedDate,
        string? reason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageIdentifierHistoryService : IManageIdentifierHistoryService
{
    private readonly MknDbContext _db;

    public ManageIdentifierHistoryService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<IdentifierHistory>> ListForMemberAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        return await _db.IdentifierHistories
            .Where(h => h.MemberId == memberId)
            .OrderBy(h => h.EntryId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IdentifierHistory?> GetAsync(int entryId, CancellationToken cancellationToken = default)
    {
        return await _db.IdentifierHistories
            .FirstOrDefaultAsync(h => h.EntryId == entryId, cancellationToken);
    }

    public async Task<ConfigurationResult<IdentifierHistory>> IssueAsync(
        int memberId,
        string type,
        string number,
        DateOnly assignedDate,
        int authorizedByAdminId,
        CancellationToken cancellationToken = default)
    {
        if (!ConfigurationValidation.IsValidName(type))
        {
            return ConfigurationResult<IdentifierHistory>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Type is required."));
        }
        if (!ConfigurationValidation.IsValidName(number))
        {
            return ConfigurationResult<IdentifierHistory>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Number is required."));
        }

        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            return ConfigurationResult<IdentifierHistory>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Member does not exist."));
        }

        var admin = await _db.Admins
            .FirstOrDefaultAsync(a => a.AdminId == authorizedByAdminId, cancellationToken);
        if (admin is null)
        {
            return ConfigurationResult<IdentifierHistory>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Authorizing admin does not exist."));
        }

        var entity = new IdentifierHistory
        {
            MemberId = memberId,
            Type = type.Trim(),
            Number = number.Trim(),
            AssignedDate = assignedDate,
            UnassignedDate = null,
            Reason = null,
            AuthorizedBy = authorizedByAdminId,
        };
        _db.IdentifierHistories.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<IdentifierHistory>.Success(entity);
    }

    public async Task<ConfigurationResult<IdentifierHistory>> RetireAsync(
        int entryId,
        DateOnly unassignedDate,
        string? reason,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.IdentifierHistories
            .FirstOrDefaultAsync(h => h.EntryId == entryId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(IdentifierHistory), entryId);
        }

        if (entity.UnassignedDate is not null)
        {
            return ConfigurationResult<IdentifierHistory>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "This identifier has already been unassigned."));
        }

        if (unassignedDate < entity.AssignedDate)
        {
            return ConfigurationResult<IdentifierHistory>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "UnassignedDate cannot be before AssignedDate."));
        }

        entity.UnassignedDate = unassignedDate;
        entity.Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<IdentifierHistory>.Success(entity);
    }
}
