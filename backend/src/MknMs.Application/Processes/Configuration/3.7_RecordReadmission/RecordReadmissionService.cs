namespace MknMs.Application.Processes.Configuration.RecordReadmission;

/// <summary>
/// Implementation of subprocess 3.7 Record Readmission.
/// </summary>
/// <remarks>
/// Reads D4 (Member) and D4 (Admin). Writes D13 (Readmission). No
/// other store is touched. Member.MemberStatusId is not modified by
/// this service — a readmission records the event; any status change
/// is a separate administrative action or a separate Attendance Rule
/// outcome (§9.1.7).
///
/// Specification: §4 (Readmission), §9.1.7, §13.6, §15.8.23;
/// docs/processes/contracts.md "Record Readmission command".
/// </remarks>
public sealed class RecordReadmissionService : IRecordReadmissionService
{
    private readonly MknDbContext _db;

    public RecordReadmissionService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<ConfigurationResult<Readmission>> RecordAsync(
        RecordReadmissionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.MemberId <= 0)
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                "MemberId must be positive.");
        }

        if (command.ActingAdminId <= 0)
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                "ActingAdminId must be positive.");
        }

        var adminError = await AdminResolver.ResolveActiveAsync(
            _db, command.ActingAdminId, cancellationToken);
        if (adminError is not null)
        {
            return ConfigurationResult<Readmission>.Failure(adminError);
        }

        var memberExists = await _db.Members
            .AnyAsync(m => m.MemberId == command.MemberId, cancellationToken);
        if (!memberExists)
        {
            return Failure(
                ConfigurationErrorCodes.NotFound,
                $"Member {command.MemberId} does not exist.");
        }

        var entity = new Readmission
        {
            MemberId = command.MemberId,
            ReadmissionDate = command.ReadmissionDate,
            PerformedByAdminId = command.ActingAdminId,
            Reason = string.IsNullOrWhiteSpace(command.Reason)
                ? null
                : command.Reason.Trim(),
        };

        _db.Readmissions.Add(entity);
        await _db.SaveChangesAsync(cancellationToken);

        return ConfigurationResult<Readmission>.Success(entity);
    }

    private static ConfigurationResult<Readmission> Failure(
        string code, string message) =>
        ConfigurationResult<Readmission>.Failure(
            new ConfigurationError(code, message));
}
