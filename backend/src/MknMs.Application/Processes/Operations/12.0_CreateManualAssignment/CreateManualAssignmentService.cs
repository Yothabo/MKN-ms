using MknMs.Application.Processes.Operations.DispatchNotification;

namespace MknMs.Application.Processes.Operations.CreateManualAssignment;

/// <summary>
/// Implementation of process 12.0 Create Manual Assignment.
/// </summary>
/// <remarks>
/// Direct creation of a RosterAssignment row from an administrator's
/// selection. The one process other than 5.0 that writes a new
/// RosterAssignment row from scratch.
///
/// The contract is asymmetric: Duty Rule criteria of type Role, Gender,
/// AgeRange, Tenure, MembershipStage, and BranchAttendanceRecency are
/// bypassed. Duty Rule criteria of type EligibilityFlag are not. Slot
/// capacity and per-occurrence availability are not enforced.
/// Uniqueness on (MemberId, DutyId, OccurrenceId) is enforced by the
/// database and is a hard failure, not a soft report.
///
/// Specification: §17.
/// </remarks>
public sealed class CreateManualAssignmentService : ICreateManualAssignmentService
{
    private readonly MknDbContext _db;
    private readonly IDispatchNotificationService _notification;
    private readonly TimeProvider _clock;

    public CreateManualAssignmentService(
        MknDbContext db,
        IDispatchNotificationService notification,
        TimeProvider clock)
    {
        _db = db;
        _notification = notification;
        _clock = clock;
    }

    public async Task<CreateManualAssignmentResult> RunAsync(
        CreateManualAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Validate referents.
            var member = await _db.Members
                .FirstOrDefaultAsync(m => m.MemberId == command.MemberId, cancellationToken);
            if (member is null)
            {
                return Failure($"Member {command.MemberId} does not exist.");
            }

            var occurrence = await _db.ServiceOccurrences
                .FirstOrDefaultAsync(o => o.OccurrenceId == command.OccurrenceId, cancellationToken);
            if (occurrence is null)
            {
                return Failure($"ServiceOccurrence {command.OccurrenceId} does not exist.");
            }

            var duty = await _db.Duties
                .FirstOrDefaultAsync(d => d.DutyId == command.DutyId, cancellationToken);
            if (duty is null)
            {
                return Failure($"Duty {command.DutyId} does not exist.");
            }

            var admin = await _db.Admins
                .FirstOrDefaultAsync(a => a.AdminId == command.ActingAdminId, cancellationToken);
            if (admin is null)
            {
                return Failure($"Admin {command.ActingAdminId} does not exist.");
            }

            if (command.TargetStatusId is int statusId)
            {
                var statusExists = await _db.AssignmentStatuses
                    .AnyAsync(s => s.AssignmentStatusId == statusId, cancellationToken);
                if (!statusExists)
                {
                    return Failure($"AssignmentStatus {statusId} does not exist.");
                }
            }

            // Uniqueness: (MemberId, DutyId, OccurrenceId) must not
            // already exist. §17.1 B5.
            var duplicate = await _db.RosterAssignments
                .AnyAsync(a => a.MemberId == command.MemberId
                    && a.DutyId == command.DutyId
                    && a.OccurrenceId == command.OccurrenceId,
                    cancellationToken);

            if (duplicate)
            {
                return Failure(
                    "A RosterAssignment already exists for this member, duty, and occurrence.");
            }

            // Eligibility Flag check. §17.1 B2. If any active DutyRule
            // for the duty has CriteriaType = 'EligibilityFlag', the
            // selected member must hold a current, un-revoked
            // Eligibility grant for the duty.
            var eligibilityFlagRules = await _db.DutyRules
                .Where(r => r.DutyId == command.DutyId
                    && r.CriteriaType == "EligibilityFlag"
                    && r.IsActive)
                .ToListAsync(cancellationToken);

            if (eligibilityFlagRules.Count > 0)
            {
                // The rule's CriteriaValue names the duty the member
                // must be eligible for. If the value is not an integer,
                // the rule cannot be resolved and the manual assignment
                // is rejected — the same reading 5.0 applies.
                foreach (var rule in eligibilityFlagRules)
                {
                    if (!int.TryParse(rule.CriteriaValue, out var requiredDutyId))
                    {
                        return Failure(
                            $"Eligibility Flag rule {rule.RuleId} has a non-integer CriteriaValue.");
                    }

                    var hasEligibility = await _db.Eligibilities
                        .AnyAsync(e => e.MemberId == command.MemberId
                            && e.DutyId == requiredDutyId
                            && e.RevokedDate == null,
                            cancellationToken);

                    if (!hasEligibility)
                    {
                        return Failure(
                            $"Member {command.MemberId} does not hold a current Eligibility " +
                            $"grant for the duty referenced by rule {rule.RuleId}.");
                    }
                }
            }

            // Create the row. Status is whatever the admin selected,
            // including NULL. Provenance is Manual.
            var assignment = new RosterAssignment
            {
                MemberId = command.MemberId,
                DutyId = command.DutyId,
                OccurrenceId = command.OccurrenceId,
                AssignmentStatusId = command.TargetStatusId,
                ApprovedBy = null,
                AssignmentSource = "Manual",
                AssignedBy = command.ActingAdminId,
                CreatedAt = _clock.GetUtcNow(),
            };

            _db.RosterAssignments.Add(assignment);
            await _db.SaveChangesAsync(cancellationToken);

            var notified = false;

            // 9.0 invoked only when the created row's status is null.
            // §17.1 C2.
            if (command.TargetStatusId is null)
            {
                await _notification.DispatchAssignmentNoticeAsync(assignment, cancellationToken);
                notified = true;
            }

            return new CreateManualAssignmentResult
            {
                Status = "Success",
                AssignmentId = assignment.AssignmentId,
                NotificationDispatched = notified,
            };
        }
        catch (Exception ex)
        {
            return new CreateManualAssignmentResult
            {
                Status = "Failure",
                AssignmentId = 0,
                NotificationDispatched = false,
                ErrorDetail = ex.Message,
            };
        }
    }

    private static CreateManualAssignmentResult Failure(string detail) =>
        new()
        {
            Status = "Failure",
            AssignmentId = 0,
            NotificationDispatched = false,
            ErrorDetail = detail,
        };
}
