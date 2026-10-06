namespace MknMs.Application.Processes.Configuration.ManageEligibility;

/// <summary>
/// Process 4.0 — Manage Eligibility.
/// </summary>
/// <remarks>
/// Eligibility is a binary, administrator-granted permission record for
/// a member against a duty, independent of Duty Rule's ranking logic.
///
/// Each grant/revoke cycle is its own row. A grant inserts a new row.
/// A revoke sets RevokedDate on an existing row. A re-grant creates
/// another new row. The EligibilityId is a durable primary key so
/// revocation references stay clean across repeated grant/revoke
/// cycles over time.
///
/// This service does not resolve which row is currently applicable for
/// a (MemberId, DutyId) pair. That resolution belongs to the reading
/// processes — 5.0 Generate Assignment and 12.0 Create Manual
/// Assignment — per §9.1.9 and §10.1.4.
///
/// Specification: §4 (Eligibility), §5 (EligibilityFlag), §9.1.9, §10.1.4.
/// </remarks>
public interface IManageEligibilityService
{
    /// <summary>
    /// Returns every Eligibility row for the given member/duty pair, in
    /// chronological order of grant. The caller (5.0 or 12.0) resolves
    /// the applicable row.
    /// </summary>
    Task<IReadOnlyList<Eligibility>> ListForMemberAndDutyAsync(
        int memberId,
        int dutyId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns every Eligibility row for the given member, across all
    /// duties.
    /// </summary>
    Task<IReadOnlyList<Eligibility>> ListForMemberAsync(
        int memberId,
        CancellationToken cancellationToken = default);

    Task<Eligibility?> GetAsync(int eligibilityId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Creates a new grant row. Does not check for an existing active
    /// grant — the specification treats the absence of a revoke on the
    /// most recent grant as "the member is eligible", and multiple
    /// active grants for the same pair are permitted by the schema.
    /// </summary>
    Task<ConfigurationResult<Eligibility>> GrantAsync(
        int memberId,
        int dutyId,
        DateOnly grantedDate,
        int grantedByAdminId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Revokes an existing grant row by setting RevokedDate and
    /// RevokedReason. The row is not deleted.
    /// </summary>
    Task<ConfigurationResult<Eligibility>> RevokeAsync(
        int eligibilityId,
        DateOnly revokedDate,
        string? revokedReason,
        CancellationToken cancellationToken = default);
}

public sealed class ManageEligibilityService : IManageEligibilityService
{
    private readonly MknDbContext _db;

    public ManageEligibilityService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Eligibility>> ListForMemberAndDutyAsync(
        int memberId,
        int dutyId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Eligibilities
            .Where(e => e.MemberId == memberId && e.DutyId == dutyId)
            .OrderBy(e => e.GrantedDate)
            .ThenBy(e => e.EligibilityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Eligibility>> ListForMemberAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        return await _db.Eligibilities
            .Where(e => e.MemberId == memberId)
            .OrderBy(e => e.DutyId)
            .ThenBy(e => e.GrantedDate)
            .ThenBy(e => e.EligibilityId)
            .ToListAsync(cancellationToken);
    }

    public async Task<Eligibility?> GetAsync(int eligibilityId, CancellationToken cancellationToken = default)
    {
        return await _db.Eligibilities
            .FirstOrDefaultAsync(e => e.EligibilityId == eligibilityId, cancellationToken);
    }

    public async Task<ConfigurationResult<Eligibility>> GrantAsync(
        int memberId,
        int dutyId,
        DateOnly grantedDate,
        int grantedByAdminId,
        CancellationToken cancellationToken = default)
    {
        var member = await _db.Members
            .FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);
        if (member is null)
        {
            return ConfigurationResult<Eligibility>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Member does not exist."));
        }

        var duty = await _db.Duties
            .FirstOrDefaultAsync(d => d.DutyId == dutyId, cancellationToken);
        if (duty is null)
        {
            return ConfigurationResult<Eligibility>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Duty does not exist."));
        }

        var admin = await _db.Admins
            .FirstOrDefaultAsync(a => a.AdminId == grantedByAdminId, cancellationToken);
        if (admin is null)
        {
            return ConfigurationResult<Eligibility>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                    "Granting admin does not exist."));
        }

        var entity = new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = grantedDate,
            GrantedBy = grantedByAdminId,
            RevokedDate = null,
            RevokedReason = null,
        };
        _db.Eligibilities.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Eligibility>.Success(entity);
    }

    public async Task<ConfigurationResult<Eligibility>> RevokeAsync(
        int eligibilityId,
        DateOnly revokedDate,
        string? revokedReason,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Eligibilities
            .FirstOrDefaultAsync(e => e.EligibilityId == eligibilityId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Eligibility), eligibilityId);
        }

        if (entity.RevokedDate is not null)
        {
            return ConfigurationResult<Eligibility>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive,
                    "This grant has already been revoked."));
        }

        if (revokedDate < entity.GrantedDate)
        {
            return ConfigurationResult<Eligibility>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.InvalidValue,
                    "RevokedDate cannot be before GrantedDate."));
        }

        // The row is not deleted. Revocation is expressed by setting
        // RevokedDate. History is preserved.
        entity.RevokedDate = revokedDate;
        entity.RevokedReason = string.IsNullOrWhiteSpace(revokedReason) ? null : revokedReason.Trim();
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Eligibility>.Success(entity);
    }
}
