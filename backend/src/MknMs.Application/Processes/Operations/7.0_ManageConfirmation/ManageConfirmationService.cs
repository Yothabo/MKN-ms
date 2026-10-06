using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D11_SystemSetting;

namespace MknMs.Application.Processes.Operations.ManageConfirmation;

/// <summary>
/// Implementation of process 7.0 Manage Confirmation.
/// </summary>
/// <remarks>
/// Owns the assignment status lifecycle from where 5.0 leaves off.
/// 5.0 creates RosterAssignment rows with AssignmentStatusId = NULL.
/// 7.0 transitions them to administrator-designated statuses and, when
/// a transition lands on a terminal status, re-resolves the affected
/// (OccurrenceId, DutyId) slot by running the same candidate selection
/// 5.0 uses, scoped to that slot.
///
/// The service does not enforce a status state machine — §11.1.3 states
/// that any transition is permitted at the schema level and that the
/// lifecycle semantics are the administrator's concern. The service
/// records the target status the caller supplied and observes whether
/// it is terminal, which determines whether re-resolution runs.
///
/// Invokes 9.0 Dispatch Notification on each replacement created.
/// Fire-and-forget: the notification's success or failure does not
/// affect the replacement's existence.
///
/// Specification: §11.
/// </remarks>
public sealed class ManageConfirmationService : IManageConfirmationService
{
    private const string InitialAssignmentStatusIdKey = "InitialAssignmentStatusID";
    private const string ConfirmationTimeoutHoursKey = "ConfirmationTimeoutHours";
    private const string TenureThresholdKey = "TenureThresholdDays";
    private const string AgeRangeMinKey = "AgeRangeMin";
    private const string AgeRangeMaxKey = "AgeRangeMax";

    private readonly MknDbContext _db;
    private readonly IDispatchNotificationService _notification;
    private readonly TimeProvider _clock;

    public ManageConfirmationService(
        MknDbContext db,
        IDispatchNotificationService notification,
        TimeProvider clock)
    {
        _db = db;
        _notification = notification;
        _clock = clock;
    }

    public async Task<ManageConfirmationResult> RunAsync(
        ManageConfirmationCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            return new ManageConfirmationResult
            {
                Status = "Failure",
                AssignmentsTransitioned = 0,
                ReplacementsCreated = 0,
                SlotsLeftVacant = 0,
                ErrorDetail = "Command must be either a Respond (AssignmentId and " +
                              "TargetStatusId) or a Sweep (IsSweep and TargetStatusId).",
            };
        }

        try
        {
            // Required-setting check. §11.1.5: 7.0 refuses to run if
            // InitialAssignmentStatusID is unset or invalid. The setting
            // is loaded but not used directly here — it is validated so
            // the service refuses to run when it is absent, matching the
            // required-setting rule. For a Respond command, the caller
            // supplies the target status; for a Sweep command, the same
            // applies. What matters for the refusal is the setting being
            // present, since §11.1.5 names it as required.
            var initialStatusId = await ReadIntSettingAsync(InitialAssignmentStatusIdKey, cancellationToken);
            if (initialStatusId is null)
            {
                return new ManageConfirmationResult
                {
                    Status = "Failure",
                    AssignmentsTransitioned = 0,
                    ReplacementsCreated = 0,
                    SlotsLeftVacant = 0,
                    ErrorDetail = $"Required setting '{InitialAssignmentStatusIdKey}' is not configured.",
                };
            }

            // Validate the target status exists.
            var targetStatus = await _db.AssignmentStatuses
                .FirstOrDefaultAsync(s => s.AssignmentStatusId == command.TargetStatusId!.Value,
                    cancellationToken);

            if (targetStatus is null)
            {
                return new ManageConfirmationResult
                {
                    Status = "Failure",
                    AssignmentsTransitioned = 0,
                    ReplacementsCreated = 0,
                    SlotsLeftVacant = 0,
                    ErrorDetail = $"AssignmentStatus {command.TargetStatusId} does not exist.",
                };
            }

            if (command.IsRespond)
            {
                return await RespondAsync(
                    command.AssignmentId!.Value,
                    targetStatus,
                    cancellationToken);
            }

            return await SweepAsync(targetStatus, cancellationToken);
        }
        catch (Exception ex)
        {
            return new ManageConfirmationResult
            {
                Status = "Failure",
                AssignmentsTransitioned = 0,
                ReplacementsCreated = 0,
                SlotsLeftVacant = 0,
                ErrorDetail = ex.Message,
            };
        }
    }

    // -----------------------------------------------------------------
    // Respond — one assignment
    // -----------------------------------------------------------------

    private async Task<ManageConfirmationResult> RespondAsync(
        int assignmentId,
        AssignmentStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var assignment = await _db.RosterAssignments
            .FirstOrDefaultAsync(a => a.AssignmentId == assignmentId, cancellationToken);

        if (assignment is null)
        {
            throw new EntityNotFoundException(nameof(RosterAssignment), assignmentId);
        }

        assignment.AssignmentStatusId = targetStatus.AssignmentStatusId;
        await _db.SaveChangesAsync(cancellationToken);

        var replacements = 0;
        var vacant = 0;

        if (targetStatus.IsTerminal)
        {
            var reResolved = await ReResolveAsync(assignment, cancellationToken);
            if (reResolved)
            {
                replacements++;
            }
            else
            {
                vacant++;
            }
        }

        return new ManageConfirmationResult
        {
            Status = "Success",
            AssignmentsTransitioned = 1,
            ReplacementsCreated = replacements,
            SlotsLeftVacant = vacant,
        };
    }

    // -----------------------------------------------------------------
    // Sweep — timed-out assignments
    // -----------------------------------------------------------------

    private async Task<ManageConfirmationResult> SweepAsync(
        AssignmentStatus targetStatus,
        CancellationToken cancellationToken)
    {
        // §11.1.6: an assignment is eligible for timeout when its
        // elapsed age reaches the configured hours. If the setting is
        // required and missing, the service refuses to run. If the
        // setting is absent and Required = false, no timeout occurs.
        var timeoutSetting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == ConfirmationTimeoutHoursKey, cancellationToken);

        if (timeoutSetting is null || timeoutSetting.Value is null)
        {
            if (timeoutSetting?.Required ?? false)
            {
                return new ManageConfirmationResult
                {
                    Status = "Failure",
                    AssignmentsTransitioned = 0,
                    ReplacementsCreated = 0,
                    SlotsLeftVacant = 0,
                    ErrorDetail = $"Required setting '{ConfirmationTimeoutHoursKey}' is not configured.",
                };
            }

            // Optional and absent: no timeout occurs. The sweep is a
            // no-op success.
            return new ManageConfirmationResult
            {
                Status = "Success",
                AssignmentsTransitioned = 0,
                ReplacementsCreated = 0,
                SlotsLeftVacant = 0,
            };
        }

        if (!int.TryParse(timeoutSetting.Value, out var timeoutHours) || timeoutHours < 0)
        {
            return new ManageConfirmationResult
            {
                Status = "Failure",
                AssignmentsTransitioned = 0,
                ReplacementsCreated = 0,
                SlotsLeftVacant = 0,
                ErrorDetail = $"Setting '{ConfirmationTimeoutHoursKey}' must be a non-negative integer.",
            };
        }

        var now = _clock.GetUtcNow();
        var cutoff = now.AddHours(-timeoutHours);

        // Every NULL-status assignment whose CreatedAt is at or before
        // the cutoff. This is the "waiting for a response past the
        // configured window" set.
        var timedOut = await _db.RosterAssignments
            .Where(a => a.AssignmentStatusId == null && a.CreatedAt <= cutoff)
            .OrderBy(a => a.AssignmentId)
            .ToListAsync(cancellationToken);

        if (timedOut.Count == 0)
        {
            return new ManageConfirmationResult
            {
                Status = "Success",
                AssignmentsTransitioned = 0,
                ReplacementsCreated = 0,
                SlotsLeftVacant = 0,
            };
        }

        var transitions = 0;
        var replacements = 0;
        var vacant = 0;

        foreach (var assignment in timedOut)
        {
            assignment.AssignmentStatusId = targetStatus.AssignmentStatusId;
            await _db.SaveChangesAsync(cancellationToken);
            transitions++;

            if (targetStatus.IsTerminal)
            {
                var reResolved = await ReResolveAsync(assignment, cancellationToken);
                if (reResolved)
                {
                    replacements++;
                }
                else
                {
                    vacant++;
                }
            }
        }

        return new ManageConfirmationResult
        {
            Status = "Success",
            AssignmentsTransitioned = transitions,
            ReplacementsCreated = replacements,
            SlotsLeftVacant = vacant,
        };
    }

    // -----------------------------------------------------------------
    // Re-resolution — scoped to the affected (OccurrenceId, DutyId)
    // -----------------------------------------------------------------

    /// <summary>
    /// Attempt to refill the affected (OccurrenceId, DutyId) slot after
    /// a terminal transition. Returns true if a replacement assignment
    /// was created, false if no candidate was found.
    /// </summary>
    /// <remarks>
    /// §11.1.7: the candidate set excludes any member who has held a
    /// terminal assignment on this specific slot. That is derived from
    /// existing RosterAssignment rows — no new field or table.
    /// </remarks>
    private async Task<bool> ReResolveAsync(
        RosterAssignment terminalAssignment,
        CancellationToken cancellationToken)
    {
        var occurrenceId = terminalAssignment.OccurrenceId;
        var dutyId = terminalAssignment.DutyId;

        // The duty's effective required slot count. If the occurrence
        // does not carry this duty, re-resolution is skipped — there is
        // nothing to refill.
        var requiredSlots = await ResolveRequiredSlotCountAsync(occurrenceId, dutyId, cancellationToken);
        if (requiredSlots is null)
        {
            return false;
        }

        // Existing non-terminal assignments on this slot. If the slot is
        // still occupied at or above its required count, re-resolution
        // is skipped — the terminal transition did not create a vacancy.
        var nonTerminalCount = await CountNonTerminalAsync(occurrenceId, dutyId, cancellationToken);
        if (nonTerminalCount >= requiredSlots.Value)
        {
            return false;
        }

        // Cumulative exclusion: any member holding a terminal
        // assignment on this specific slot. Derived from the rows.
        var terminalMemberIds = await _db.RosterAssignments
            .Where(a => a.OccurrenceId == occurrenceId
                && a.DutyId == dutyId
                && a.AssignmentStatusId != null)
            .Join(_db.AssignmentStatuses,
                a => a.AssignmentStatusId,
                s => s.AssignmentStatusId,
                (a, s) => new { a.MemberId, s.IsTerminal })
            .Where(x => x.IsTerminal)
            .Select(x => x.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        // Members already assigned non-terminally anywhere on this
        // occurrence — the one-member-per-occurrence rule (§10.1.2)
        // applies to re-resolution too, since it is the same candidate
        // availability rule.
        var membersOnOccurrence = await _db.RosterAssignments
            .Where(a => a.OccurrenceId == occurrenceId
                && (a.AssignmentStatusId == null
                    || _db.AssignmentStatuses
                        .Where(s => s.AssignmentStatusId == a.AssignmentStatusId)
                        .Select(s => !s.IsTerminal)
                        .FirstOrDefault()))
            .Select(a => a.MemberId)
            .Distinct()
            .ToListAsync(cancellationToken);

        var exclusion = new HashSet<int>(terminalMemberIds);
        foreach (var memberId in membersOnOccurrence)
        {
            exclusion.Add(memberId);
        }

        var today = DateOnly.FromDateTime(_clock.GetUtcNow().UtcDateTime);
        var tenureThresholdDays = await ReadIntSettingAsync(TenureThresholdKey, cancellationToken);
        var ageRangeMin = await ReadIntSettingAsync(AgeRangeMinKey, cancellationToken) ?? 0;
        var ageRangeMax = await ReadIntSettingAsync(AgeRangeMaxKey, cancellationToken) ?? 120;

        var candidates = await FindCandidatesAsync(
            occurrenceId,
            dutyId,
            exclusion,
            today,
            tenureThresholdDays,
            ageRangeMin,
            ageRangeMax,
            cancellationToken);

        if (candidates.Count == 0)
        {
            // No candidate. Slot remains vacant. No fallback. §11.1.7.
            return false;
        }

        var replacement = new RosterAssignment
        {
            MemberId = candidates[0],
            DutyId = dutyId,
            OccurrenceId = occurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        };

        _db.RosterAssignments.Add(replacement);
        await _db.SaveChangesAsync(cancellationToken);

        // 9.0 invoked only on replacement creation. Fire-and-forget.
        await _notification.DispatchAssignmentNoticeAsync(replacement, cancellationToken);

        return true;
    }

    private async Task<int?> ResolveRequiredSlotCountAsync(
        int occurrenceId,
        int dutyId,
        CancellationToken cancellationToken)
    {
        // The occurrence's schedule determines the service definition.
        var occurrence = await _db.ServiceOccurrences
            .FirstOrDefaultAsync(o => o.OccurrenceId == occurrenceId, cancellationToken);

        if (occurrence?.ScheduleId is null)
        {
            return null;
        }

        var schedule = await _db.ServiceSchedules
            .FirstOrDefaultAsync(s => s.ScheduleId == occurrence.ScheduleId.Value, cancellationToken);

        if (schedule is null)
        {
            return null;
        }

        var serviceDefId = schedule.ServiceDefId;

        // Check for a per-occurrence override first.
        var overrideRow = await _db.ServiceOccurrenceDuties
            .FirstOrDefaultAsync(d => d.OccurrenceId == occurrenceId && d.DutyId == dutyId,
                cancellationToken);

        if (overrideRow is not null)
        {
            if (overrideRow.Action == "Removed")
            {
                return null;
            }

            if (overrideRow.Action == "Added" && overrideRow.RequiredSlotCount is int count)
            {
                return count;
            }
        }

        var inherited = await _db.ServiceDefinitionDuties
            .FirstOrDefaultAsync(d => d.ServiceDefId == serviceDefId
                && d.DutyId == dutyId
                && d.IsActive,
                cancellationToken);

        return inherited?.RequiredSlotCount;
    }

    private async Task<int> CountNonTerminalAsync(
        int occurrenceId,
        int dutyId,
        CancellationToken cancellationToken)
    {
        // Null status counts as non-terminal (awaiting the initial
        // transition). A set status counts as non-terminal only when
        // IsTerminal is false.
        var nullCount = await _db.RosterAssignments
            .CountAsync(a => a.OccurrenceId == occurrenceId
                && a.DutyId == dutyId
                && a.AssignmentStatusId == null,
                cancellationToken);

        var nonTerminalWithStatus = await _db.RosterAssignments
            .Where(a => a.OccurrenceId == occurrenceId
                && a.DutyId == dutyId
                && a.AssignmentStatusId != null)
            .Join(_db.AssignmentStatuses,
                a => a.AssignmentStatusId,
                s => s.AssignmentStatusId,
                (a, s) => new { s.IsTerminal })
            .Where(x => !x.IsTerminal)
            .CountAsync(cancellationToken);

        return nullCount + nonTerminalWithStatus;
    }

    // -----------------------------------------------------------------
    // Candidate selection — the same algorithm 5.0 uses, scoped to one
    // duty, with an additional exclusion set supplied by the caller.
    // -----------------------------------------------------------------

    private async Task<List<int>> FindCandidatesAsync(
        int occurrenceId,
        int dutyId,
        HashSet<int> exclusion,
        DateOnly today,
        int? tenureThresholdDays,
        int ageRangeMin,
        int ageRangeMax,
        CancellationToken cancellationToken)
    {
        var eligibleMemberIds = await ResolveEligibleMembersAsync(dutyId, cancellationToken);
        if (eligibleMemberIds.Count == 0)
        {
            return new List<int>();
        }

        var members = await _db.Members
            .Where(m => eligibleMemberIds.Contains(m.MemberId) && m.IsActive)
            .ToListAsync(cancellationToken);

        members = members
            .Where(m => !exclusion.Contains(m.MemberId))
            .ToList();

        if (members.Count == 0)
        {
            return new List<int>();
        }

        var rules = await _db.DutyRules
            .Where(r => r.DutyId == dutyId && r.IsActive)
            .OrderBy(r => r.TierOrder)
            .ThenBy(r => r.RuleId)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return members.Select(m => m.MemberId).OrderBy(id => id).ToList();
        }

        var occurrence = await _db.ServiceOccurrences
            .FirstAsync(o => o.OccurrenceId == occurrenceId, cancellationToken);

        var tiers = rules
            .GroupBy(r => r.TierOrder)
            .OrderBy(g => g.Key)
            .ToList();

        var cache = new Dictionary<int, bool>();

        foreach (var tier in tiers)
        {
            var tierMembers = new List<int>();

            foreach (var member in members)
            {
                var matchesTier = true;

                foreach (var rule in tier)
                {
                    var matches = await EvaluateCriterionAsync(
                        member, rule.CriteriaType, rule.CriteriaValue,
                        occurrence, today, tenureThresholdDays,
                        ageRangeMin, ageRangeMax, cache, cancellationToken);

                    if (!matches)
                    {
                        matchesTier = false;
                        break;
                    }
                }

                if (matchesTier)
                {
                    tierMembers.Add(member.MemberId);
                }
            }

            if (tierMembers.Count > 0)
            {
                return tierMembers.OrderBy(id => id).ToList();
            }
        }

        return new List<int>();
    }

    private async Task<HashSet<int>> ResolveEligibleMembersAsync(
        int dutyId,
        CancellationToken cancellationToken)
    {
        var rows = await _db.Eligibilities
            .Where(e => e.DutyId == dutyId && e.RevokedDate == null)
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(e => e.MemberId)
            .Select(g => g
                .OrderByDescending(e => e.GrantedDate)
                .ThenByDescending(e => e.EligibilityId)
                .First())
            .Where(e => e.RevokedDate == null)
            .Select(e => e.MemberId)
            .ToHashSet();
    }

    private async Task<bool> EvaluateCriterionAsync(
        Member member,
        string criteriaType,
        string criteriaValue,
        ServiceOccurrence occurrence,
        DateOnly today,
        int? tenureThresholdDays,
        int ageRangeMin,
        int ageRangeMax,
        Dictionary<int, bool> cache,
        CancellationToken cancellationToken)
    {
        switch (criteriaType)
        {
            case "Role":
                var role = await _db.Roles
                    .FirstOrDefaultAsync(r => r.Name == criteriaValue, cancellationToken);
                return role is not null && member.RoleId == role.RoleId;

            case "Gender":
                return string.Equals(member.Gender, criteriaValue, StringComparison.OrdinalIgnoreCase);

            case "AgeRange":
                return EvaluateAgeRange(member.DateOfBirth, criteriaValue, today, ageRangeMin, ageRangeMax);

            case "Tenure":
                return EvaluateTenure(member.JoinDate, today, tenureThresholdDays, criteriaValue);

            case "MembershipStage":
                return string.Equals(member.MembershipStage, criteriaValue, StringComparison.OrdinalIgnoreCase);

            case "BranchAttendanceRecency":
                if (!int.TryParse(criteriaValue, out var windowDays))
                {
                    return false;
                }
                var cutoff = DateTimeOffset.UtcNow.AddDays(-windowDays);
                return await _db.AttendanceRecords
                    .AnyAsync(a => a.MemberId == member.MemberId && a.Timestamp >= cutoff,
                        cancellationToken);

            case "EligibilityFlag":
                if (!int.TryParse(criteriaValue, out var dutyId))
                {
                    return false;
                }
                return await _db.Eligibilities
                    .AnyAsync(e => e.MemberId == member.MemberId
                        && e.DutyId == dutyId
                        && e.RevokedDate == null,
                        cancellationToken);

            case "AcceptanceRate":
            case "DutiesCarried":
            case "DaysSinceLastAssignment":
                return false;

            default:
                return false;
        }
    }

    private static bool EvaluateAgeRange(
        DateOnly dateOfBirth,
        string criteriaValue,
        DateOnly today,
        int fallbackMin,
        int fallbackMax)
    {
        int min, max;
        var parts = criteriaValue.Split('-',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2
            && int.TryParse(parts[0], out var lo)
            && int.TryParse(parts[1], out var hi))
        {
            min = lo; max = hi;
        }
        else
        {
            min = fallbackMin; max = fallbackMax;
        }

        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age >= min && age <= max;
    }

    private static bool EvaluateTenure(
        DateOnly joinDate,
        DateOnly today,
        int? tenureThresholdDays,
        string criteriaValue)
    {
        int threshold;
        if (int.TryParse(criteriaValue, out var parsed))
        {
            threshold = parsed;
        }
        else if (tenureThresholdDays is int t)
        {
            threshold = t;
        }
        else
        {
            return true;
        }

        return (today.DayNumber - joinDate.DayNumber) >= threshold;
    }

    // -----------------------------------------------------------------
    // System settings
    // -----------------------------------------------------------------

    private async Task<int?> ReadIntSettingAsync(string key, CancellationToken cancellationToken)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting?.Value is null)
        {
            return null;
        }

        return int.TryParse(setting.Value, out var value) ? value : null;
    }
}
