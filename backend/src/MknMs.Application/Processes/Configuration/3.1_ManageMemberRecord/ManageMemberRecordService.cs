namespace MknMs.Application.Processes.Configuration.ManageMemberRecord;

/// <summary>
/// Subprocess 3.1 — Manage Member Record.
/// </summary>
/// <remarks>
/// Creates, edits, deactivates, and lists Member rows. A member belongs
/// to exactly one Branch and holds exactly one Role. Foreign keys are
/// validated against existing (and active) rows.
///
/// Required at creation: JoinDate, DateOfBirth, MembershipStage, Name,
/// Surname, Gender, Phone, BranchId, RoleId. Email is optional.
/// MembershipStage is free text, not a lookup.
///
/// Specification: §4 (Member), §9.1.7.
/// </remarks>
public interface IManageMemberRecordService
{
    Task<IReadOnlyList<Member>> ListAsync(
        int? branchId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default);

    Task<Member?> GetAsync(int memberId, CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Member>> CreateAsync(
        CreateMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Member>> UpdateAsync(
        int memberId,
        UpdateMemberRequest request,
        CancellationToken cancellationToken = default);

    Task<ConfigurationResult<Member>> DeactivateAsync(
        int memberId,
        CancellationToken cancellationToken = default);
}

public sealed record CreateMemberRequest
{
    public required string Name { get; init; }
    public required string Surname { get; init; }
    public required DateOnly JoinDate { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required string MembershipStage { get; init; }
    public required string Gender { get; init; }
    public required string Phone { get; init; }
    public string? Email { get; init; }
    public required int BranchId { get; init; }
    public required int RoleId { get; init; }
}

public sealed record UpdateMemberRequest
{
    public required string Name { get; init; }
    public required string Surname { get; init; }
    public required DateOnly DateOfBirth { get; init; }
    public required string MembershipStage { get; init; }
    public required string Gender { get; init; }
    public required string Phone { get; init; }
    public string? Email { get; init; }
    public required int BranchId { get; init; }
    public required int RoleId { get; init; }
}

public sealed class ManageMemberRecordService : IManageMemberRecordService
{
    private readonly MknDbContext _db;

    public ManageMemberRecordService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<IReadOnlyList<Member>> ListAsync(
        int? branchId = null,
        bool includeInactive = false,
        CancellationToken cancellationToken = default)
    {
        var query = _db.Members.AsQueryable();
        if (branchId is int id)
        {
            query = query.Where(m => m.BranchId == id);
        }
        if (!includeInactive)
        {
            query = query.Where(m => m.IsActive);
        }
        return await query.OrderBy(m => m.MemberId).ToListAsync(cancellationToken);
    }

    public async Task<Member?> GetAsync(int memberId, CancellationToken cancellationToken = default)
    {
        return await _db.Members.FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);
    }

    public async Task<ConfigurationResult<Member>> CreateAsync(
        CreateMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateMemberFieldsAsync(
            request.Name, request.Surname, request.MembershipStage,
            request.Gender, request.Phone, request.BranchId, request.RoleId,
            cancellationToken);

        if (validation is not null)
        {
            return ConfigurationResult<Member>.Failure(validation);
        }

        var entity = new Member
        {
            Name = request.Name.Trim(),
            Surname = request.Surname.Trim(),
            JoinDate = request.JoinDate,
            DateOfBirth = request.DateOfBirth,
            MembershipStage = request.MembershipStage.Trim(),
            Gender = request.Gender.Trim(),
            Phone = request.Phone.Trim(),
            Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim(),
            BranchId = request.BranchId,
            RoleId = request.RoleId,
            IsActive = true,
        };
        _db.Members.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Member>.Success(entity);
    }

    public async Task<ConfigurationResult<Member>> UpdateAsync(
        int memberId,
        UpdateMemberRequest request,
        CancellationToken cancellationToken = default)
    {
        var validation = await ValidateMemberFieldsAsync(
            request.Name, request.Surname, request.MembershipStage,
            request.Gender, request.Phone, request.BranchId, request.RoleId,
            cancellationToken);

        if (validation is not null)
        {
            return ConfigurationResult<Member>.Failure(validation);
        }

        var entity = await _db.Members.FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Member), memberId);
        }

        // Changing BranchId or RoleId affects only future use. Existing
        // operational records (assignments, attendance) are not touched
        // — they anchor to occurrences, not to the member's attributes.
        entity.Name = request.Name.Trim();
        entity.Surname = request.Surname.Trim();
        entity.DateOfBirth = request.DateOfBirth;
        entity.MembershipStage = request.MembershipStage.Trim();
        entity.Gender = request.Gender.Trim();
        entity.Phone = request.Phone.Trim();
        entity.Email = string.IsNullOrWhiteSpace(request.Email) ? null : request.Email.Trim();
        entity.BranchId = request.BranchId;
        entity.RoleId = request.RoleId;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Member>.Success(entity);
    }

    public async Task<ConfigurationResult<Member>> DeactivateAsync(
        int memberId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _db.Members.FirstOrDefaultAsync(m => m.MemberId == memberId, cancellationToken);
        if (entity is null)
        {
            throw new EntityNotFoundException(nameof(Member), memberId);
        }

        if (!entity.IsActive)
        {
            return ConfigurationResult<Member>.Failure(
                new ConfigurationError(ConfigurationErrorCodes.AlreadyInactive, "Member is already inactive."));
        }

        entity.IsActive = false;
        await _db.SaveChangesAsync(cancellationToken);
        return ConfigurationResult<Member>.Success(entity);
    }

    private async Task<ConfigurationError?> ValidateMemberFieldsAsync(
        string name,
        string surname,
        string membershipStage,
        string gender,
        string phone,
        int branchId,
        int roleId,
        CancellationToken cancellationToken)
    {
        if (!ConfigurationValidation.IsValidName(name))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Name is required.");
        }
        if (!ConfigurationValidation.IsValidName(surname))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Surname is required.");
        }
        if (!ConfigurationValidation.IsValidName(membershipStage))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "MembershipStage is required.");
        }
        if (!ConfigurationValidation.IsValidName(gender))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Gender is required.");
        }
        if (!ConfigurationValidation.IsValidName(phone))
        {
            return new ConfigurationError(ConfigurationErrorCodes.InvalidValue, "Phone is required.");
        }

        var branch = await _db.Branches.FirstOrDefaultAsync(b => b.BranchId == branchId, cancellationToken);
        if (branch is null)
        {
            return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                "Branch does not exist.");
        }

        var role = await _db.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId, cancellationToken);
        if (role is null)
        {
            return new ConfigurationError(ConfigurationErrorCodes.ReferencedEntityMissing,
                "Role does not exist.");
        }

        return null;
    }
}
