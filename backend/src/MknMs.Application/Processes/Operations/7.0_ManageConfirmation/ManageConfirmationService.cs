using Microsoft.Extensions.Logging;
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
/// §11.1.3: no state machine. Any transition is permitted at the schema
/// level. The service records the target status the caller supplied and
/// observes whether it is terminal. Two consequences follow from
/// reading §11.1.3 together with §11.3, and neither restricts which
/// statuses may follow which:
///
///   - Re-resolution runs only when a transition lands on a terminal
///     status. A move from one terminal status to another (for example
///     Declined after TimedOut) vacates nothing and is not counted as
///     a vacancy.
///
///   - §11.3 requires a duty's non-terminal count never to exceed its
///     required slot count. A transition from a terminal status back
///     to a non-terminal one would add to that count. If the slot is
///     already full, the transition is rejected and nothing is
///     written.
///
/// Sweep and past occurrences: the timeout transition is recorded as
/// §11.1.6 states. If the occurrence's date is before today in the
/// application timezone, no replacement is sought and no notice is
/// sent, because the service has already happened. This compares dates
/// only, using the same resolver the materializer uses.
///
/// Concurrency: the respond path and each sweep iteration take the
/// occurrence-level advisory lock before the capacity check, the
/// status write, and any re-resolution. The lock is the same one 5.0
/// and 12.0 use. Contention returns a Failure naming the occurrence;
/// the caller retries. §10.3, §11.3, B6.
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
    private const string DeclinedStatusIdKey = "DeclinedStatusID";
    private const string TimedOutStatusIdKey = "TimedOutStatusID";
    private const string ConfirmationTimeoutHoursKey = "ConfirmationTimeoutHours";


    private readonly MknDbContext _db;
    private readonly IDispatchNotificationService _notification;
    private readonly TimeProvider _clock;
    private readonly IOccurrenceLockService _occurrenceLock;
    private readonly ILogger<ManageConfirmationService> _logger;

    public ManageConfirmationService(
        MknDbContext db,
        IDispatchNotificationService notification,
        TimeProvider clock,
        IOccurrenceLockService occurrenceLock,
        ILogger<ManageConfirmationService> logger)
    {
        _db = db;
        _notification = notification;
        _clock = clock;
        _occurrenceLock = occurrenceLock;
        _logger = logger;
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
                ErrorDetail = "Command must name a valid operation: Confirm or Decline " +
                              "with a positive AssignmentId, or Sweep with no AssignmentId.",
            };
        }

        try
        {
            // Resolve the target status from the administrator-designated
            // setting for this operation. §11.1.5: a confirm moves to
            // InitialAssignmentStatusID. §11.1.7: a decline moves to the
            // declined status; a timeout moves to the timed-out status.
            // All three are stored as SystemSetting, per §15's amendment
            // set as completed.
            var targetStatusId = await ResolveTargetStatusIdAsync(command.Operation, cancellationToken);

            if (targetStatusId is null)
            {
                var settingName = SettingNameForOperation(command.Operation);
                return new ManageConfirmationResult
                {
                    Status = "Failure",
                    AssignmentsTransitioned = 0,
                    ReplacementsCreated = 0,
                    SlotsLeftVacant = 0,
                    ErrorDetail = $"Required setting '{settingName}' is not configured.",
                };
            }

            var targetStatus = await _db.AssignmentStatuses
                .FirstOrDefaultAsync(s => s.AssignmentStatusId == targetStatusId.Value,
                    cancellationToken);

            if (targetStatus is null)
            {
                return new ManageConfirmationResult
                {
                    Status = "Failure",
                    AssignmentsTransitioned = 0,
                    ReplacementsCreated = 0,
                    SlotsLeftVacant = 0,
                    ErrorDetail = $"AssignmentStatus {targetStatusId} does not exist.",
                };
            }

            if (command.Operation == ManageConfirmationOperation.Sweep)
            {
                return await SweepAsync(targetStatus, cancellationToken);
            }

            return await RespondAsync(
                command.AssignmentId!.Value,
                targetStatus,
                cancellationToken);
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

    private static string SettingNameForOperation(ManageConfirmationOperation operation) =>
        operation switch
        {
            ManageConfirmationOperation.Confirm => InitialAssignmentStatusIdKey,
            ManageConfirmationOperation.Decline => DeclinedStatusIdKey,
            ManageConfirmationOperation.Sweep => TimedOutStatusIdKey,
            _ => throw new InvalidOperationException($"Unknown operation {operation}."),
        };

    private async Task<int?> ResolveTargetStatusIdAsync(
        ManageConfirmationOperation operation,
        CancellationToken cancellationToken)
    {
        var key = SettingNameForOperation(operation);
        var value = await ReadIntSettingAsync(key, cancellationToken);

        if (value is null)
        {
            return null;
        }

        // §11.1.5 and §11.1.7: the status must be a real row.
        var exists = await _db.AssignmentStatuses
            .AnyAsync(s => s.AssignmentStatusId == value.Value, cancellationToken);

        return exists ? value : null;
    }

    // -----------------------------------------------------------------
    // Respond — a member confirmed or declined
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

        // B6, §10.3, §11.3: participate in the same occurrence-level
        // lock 5.0 and 12.0 use. The lock is held across the B3
        // capacity check, the status write, and any re-resolution, so
        // the whole respond operation for this occurrence is
        // serialized against concurrent writers. Contention returns a
        // Failure naming the occurrence; the caller retries.
        await using var lease = await _occurrenceLock.TryAcquireAsync(
            assignment.OccurrenceId, cancellationToken);

        if (lease is null)
        {
            return new ManageConfirmationResult
            {
                Status = "Failure",
                AssignmentsTransitioned = 0,
                ReplacementsCreated = 0,
                SlotsLeftVacant = 0,
                ErrorDetail =
                    $"Another writer is filling occurrence {assignment.OccurrenceId}. "
                    + "Retry the confirmation response.",
            };
        }

        return await ApplyResponseUnderLockAsync(
            assignment, targetStatus, cancellationToken);
    }

    /// <summary>
    /// The body of a respond operation. Assumes the caller holds the
    /// occurrence-level advisory lock for the assignment's occurrence.
    /// </summary>
    private async Task<ManageConfirmationResult> ApplyResponseUnderLockAsync(
        RosterAssignment assignment,
        AssignmentStatus targetStatus,
        CancellationToken cancellationToken)
    {
        var assignmentId = assignment.AssignmentId;

        // §11.3 capacity invariant applied to a sequential transition.
        // §11.1.3 permits any transition, so this is not a state-machine
        // restriction; it refuses only a terminal-to-non-terminal move
        // that would push the slot's live count above its required
        // count. Non-terminal-to-anything, terminal-to-terminal, and
        // terminal-to-non-terminal-when-there-is-room are all allowed.
        var wasTerminal = false;
        if (assignment.AssignmentStatusId is int previousStatusId)
        {
            wasTerminal = await _db.AssignmentStatuses
                .Where(s => s.AssignmentStatusId == previousStatusId)
                .Select(s => s.IsTerminal)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (wasTerminal && !targetStatus.IsTerminal)
        {
            var requiredSlots = await ResolveRequiredSlotCountAsync(
                assignment.OccurrenceId, assignment.DutyId, cancellationToken);

            if (requiredSlots is not null)
            {
                var liveCount = await CountNonTerminalAsync(
                    assignment.OccurrenceId, assignment.DutyId, cancellationToken);

                if (liveCount >= requiredSlots.Value)
                {
                    return new ManageConfirmationResult
                    {
                        Status = "Failure",
                        AssignmentsTransitioned = 0,
                        ReplacementsCreated = 0,
                        SlotsLeftVacant = 0,
                        ErrorDetail =
                            $"Assignment {assignmentId} cannot move to '{targetStatus.Name}': " +
                            "its slot is already filled by another member.",
                    };
                }
            }
        }

        assignment.AssignmentStatusId = targetStatus.AssignmentStatusId;
        await _db.SaveChangesAsync(cancellationToken);

        var replacements = 0;
        var vacant = 0;

        if (targetStatus.IsTerminal)
        {
            var timeZoneResult = await TimeZoneResolver.ResolveAsync(_db, cancellationToken);
            if (!timeZoneResult.IsSuccess)
            {
                return new ManageConfirmationResult
                {
                    Status = "Failure",
                    AssignmentsTransitioned = 1,
                    ReplacementsCreated = 0,
                    SlotsLeftVacant = 0,
                    ErrorDetail = timeZoneResult.ErrorDetail,
                };
            }

            var reResolved = await ReResolveAsync(assignment, timeZoneResult.Zone!, cancellationToken);
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

        // Every NULL-status assignment at or past the window, carrying
        // its occurrence's date so the re-resolution decision can be
        // made per row. The transition itself is always recorded; the
        // occurrence's date only governs whether a replacement is
        // sought.
        var timedOut = await _db.RosterAssignments
            .Where(a => a.AssignmentStatusId == null && a.CreatedAt <= cutoff)
            .Select(a => new
            {
                Assignment = a,
                OccurrenceDate = _db.ServiceOccurrences
                    .Where(o => o.OccurrenceId == a.OccurrenceId)
                    .Select(o => o.Date)
                    .First(),
            })
            .OrderBy(x => x.Assignment.AssignmentId)
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

        // Date-level guard: an occurrence dated before today is in the
        // past in the application timezone. The timeout transition is
        // still recorded (the member did not respond), but no
        // replacement is sought and no notice goes out — the service
        // has already happened. This compares dates only. It does not
        // add a setting and it does not assume when any service runs.
        var timeZoneResult = await TimeZoneResolver.ResolveAsync(_db, cancellationToken);
        if (!timeZoneResult.IsSuccess)
        {
            return new ManageConfirmationResult
            {
                Status = "Failure",
                AssignmentsTransitioned = 0,
                ReplacementsCreated = 0,
                SlotsLeftVacant = 0,
                ErrorDetail = timeZoneResult.ErrorDetail,
            };
        }

        var timeZone = timeZoneResult.Zone!;
        var today = TimeZoneResolver.ToDateInTimeZone(_clock.GetUtcNow(), timeZone);

        var transitions = 0;
        var replacements = 0;
        var vacant = 0;

        foreach (var item in timedOut)
        {
            var assignment = item.Assignment;

            // B6, §10.3, §11.3: each sweep iteration takes the
            // occurrence-level lock before its transition and any
            // re-resolution, so the sweep is serialized against
            // concurrent writers on the same occurrence. Contention
            // leaves the assignment NULL; the next sweep picks it up.
            await using var lease = await _occurrenceLock.TryAcquireAsync(
                assignment.OccurrenceId, cancellationToken);

            if (lease is null)
            {
                // Another writer holds the occurrence. The transition
                // is not recorded; the assignment stays NULL and
                // past its timeout, so the next sweep retries it.
                continue;
            }

            assignment.AssignmentStatusId = targetStatus.AssignmentStatusId;
            await _db.SaveChangesAsync(cancellationToken);
            transitions++;

            if (!targetStatus.IsTerminal)
            {
                continue;
            }

            if (item.OccurrenceDate < today)
            {
                // The service has already happened. The transition is
                // recorded; nothing is announced and the slot is not
                // refilled.
                continue;
            }

            var reResolved = await ReResolveAsync(assignment, timeZone, cancellationToken);
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
    ///
    /// The caller must hold the occurrence-level advisory lock for the
    /// assignment's occurrence. Re-resolution no longer acquires the
    /// lock itself; the lock is held across the transition and the
    /// re-resolution as one critical section.
    /// </remarks>
    private async Task<bool> ReResolveAsync(
        RosterAssignment terminalAssignment,
        TimeZoneInfo timeZone,
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

        var today = TimeZoneResolver.ToDateInTimeZone(_clock.GetUtcNow(), timeZone);

        var candidates = await FindCandidatesAsync(
            occurrenceId,
            dutyId,
            exclusion,
            today,
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

        // 9.0 invoked only on replacement creation. §11.1.8: the
        // invocation is fire-and-forget; a failure does not roll back
        // the replacement assignment. 7.0 catches the failure and
        // logs it.
        try
        {
            await _notification.DispatchAssignmentNoticeAsync(
                replacement, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Notification dispatch failed for replacement assignment {AssignmentId}. "
                + "The replacement is committed.",
                replacement.AssignmentId);
        }

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
        CancellationToken cancellationToken)
    {
        var eligibleMemberIds = await ResolveEligibleMembersAsync(
            dutyId, today, cancellationToken);
        if (eligibleMemberIds.Count == 0)
        {
            return new List<int>();
        }

        var members = await _db.Members
            .Include(m => m.MemberStatus)
            .Where(m => eligibleMemberIds.Contains(m.MemberId)
                && m.MemberStatus.IsRosterable
                && !m.MemberStatus.IsDeleted)
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
                        occurrence, today, cancellationToken);

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

    // -----------------------------------------------------------------
    // Eligibility resolution (§10.1.4)
    // -----------------------------------------------------------------

    private async Task<HashSet<int>> ResolveEligibleMembersAsync(
        int dutyId,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // §10.1.4 as documented in the 5.0 process contract:
        //   - the row whose GrantedDate is in the past,
        //   - whose RevokedDate is either null or in the future.
        var rows = await _db.Eligibilities
            .Where(e => e.DutyId == dutyId
                && e.GrantedDate <= today
                && (e.RevokedDate == null || e.RevokedDate > today))
            .ToListAsync(cancellationToken);

        return rows
            .GroupBy(e => e.MemberId)
            .Select(g => g
                .OrderByDescending(e => e.GrantedDate)
                .ThenByDescending(e => e.EligibilityId)
                .First())
            .Select(e => e.MemberId)
            .ToHashSet();
    }

    // -----------------------------------------------------------------
    // Criteria evaluation (§5)
    // -----------------------------------------------------------------

    private async Task<bool> EvaluateCriterionAsync(
        Member member,
        string criteriaType,
        string criteriaValue,
        ServiceOccurrence occurrence,
        DateOnly today,
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
                return EvaluateAgeRange(member.DateOfBirth, criteriaValue, today);

            case "Tenure":
                return EvaluateTenure(member.JoinDate, today, criteriaValue);

            case "MembershipStage":
                return string.Equals(member.MembershipStage, criteriaValue, StringComparison.OrdinalIgnoreCase);

            case "BranchAttendanceRecency":
                // §10.1.5: "Member has an AttendanceRecord at the
                // specified branch within the configured window."
                // CriteriaValue is the window in days. The specified
                // branch is the branch of the occurrence being filled.
                return await EvaluateBranchAttendanceRecencyAsync(
                    member, criteriaValue, occurrence, cancellationToken);

            case "EligibilityFlag":
                return await EvaluateEligibilityFlagAsync(
                    member, criteriaValue, today, cancellationToken);

            case "MemberAttribute":
                return await EvaluateMemberAttributeAsync(
                    member, criteriaValue, cancellationToken);

            case "MemberStatus":
                return EvaluateMemberStatus(member, criteriaValue);

            case "Youth":
                return await EvaluateYouthAsync(member, today, cancellationToken);

            case "AcceptanceRate":
            case "DutiesCarried":
            case "DaysSinceLastAssignment":
                return false;

            default:
                return false;
        }
    }

    private async Task<bool> EvaluateMemberAttributeAsync(
        Member member,
        string criteriaValue,
        CancellationToken cancellationToken)
    {
        // §10.1.5: CriteriaValue is "AttributeTypeName=RequiredValue".
        var idx = criteriaValue.IndexOf('=');
        if (idx <= 0 || idx == criteriaValue.Length - 1)
        {
            return false;
        }

        var typeName = criteriaValue.Substring(0, idx);
        var requiredValue = criteriaValue.Substring(idx + 1);

        var attrTypeId = await _db.AttributeTypes
            .Where(a => a.Name == typeName)
            .Select(a => (int?)a.AttributeTypeId)
            .FirstOrDefaultAsync(cancellationToken);

        if (attrTypeId is null)
        {
            return false;
        }

        return await _db.MemberAttributeValues
            .AnyAsync(v => v.MemberId == member.MemberId
                && v.AttributeTypeId == attrTypeId.Value
                && v.Value == requiredValue,
                cancellationToken);
    }

    private static bool EvaluateMemberStatus(Member member, string criteriaValue)
    {
        // §10.1.5: comma-separated MemberStatusIDs, no whitespace.
        // §10.1.5: no whitespace permitted in the MemberStatus value.
        // A value containing whitespace produces no candidate (B1).
        if (criteriaValue.Any(char.IsWhiteSpace))
        {
            return false;
        }

        var parts = criteriaValue.Split(',', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 0)
        {
            return false;
        }

        foreach (var p in parts)
        {
            if (!int.TryParse(p, out var id) || id <= 0)
            {
                return false;
            }

            if (member.MemberStatusId == id)
            {
                return true;
            }
        }

        return false;
    }

    private async Task<bool> EvaluateYouthAsync(
        Member member,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // §10.1.5: Youth uses YouthAgeMin and YouthAgeMax. Both are
        // required; when either is absent, the criterion matches no
        // candidate.
        var min = await ReadIntSettingAsync("YouthAgeMin", cancellationToken);
        var max = await ReadIntSettingAsync("YouthAgeMax", cancellationToken);

        if (min is null || max is null)
        {
            return false;
        }

        var age = today.Year - member.DateOfBirth.Year;
        if (member.DateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age >= min.Value && age <= max.Value;
    }

    private async Task<bool> EvaluateBranchAttendanceRecencyAsync(
        Member member,
        string criteriaValue,
        ServiceOccurrence occurrence,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(criteriaValue, out var windowDays))
        {
            return false;
        }

        var occurrenceBranchId = await _db.ServiceSchedules
            .Where(s => s.ScheduleId == occurrence.ScheduleId)
            .Select(s => (int?)s.TimeSlot.BranchId)
            .FirstOrDefaultAsync(cancellationToken);

        if (occurrenceBranchId is null)
        {
            return false;
        }

        var cutoff = _clock.GetUtcNow().AddDays(-windowDays);

        var hasRecent = await _db.AttendanceRecords
            .Where(a => a.MemberId == member.MemberId
                && a.Timestamp >= cutoff)
            .Join(
                _db.ServiceOccurrences,
                a => a.OccurrenceId,
                o => o.OccurrenceId,
                (a, o) => new { Attendance = a, Occurrence = o })
            .Join(
                _db.ServiceSchedules,
                x => x.Occurrence.ScheduleId,
                s => (int?)s.ScheduleId,
                (x, s) => new { x.Attendance, BranchId = s.TimeSlot.BranchId })
            .AnyAsync(x => x.BranchId == occurrenceBranchId.Value, cancellationToken);

        return hasRecent;
    }

    private async Task<bool> EvaluateEligibilityFlagAsync(
        Member member,
        string criteriaValue,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(criteriaValue, out var dutyId))
        {
            return false;
        }

        // §10.1.4: the grant must be in effect today.
        var exists = await _db.Eligibilities
            .AnyAsync(e => e.MemberId == member.MemberId
                && e.DutyId == dutyId
                && e.GrantedDate <= today
                && (e.RevokedDate == null || e.RevokedDate > today),
                cancellationToken);

        return exists;
    }

    private static bool EvaluateAgeRange(
        DateOnly dateOfBirth,
        string criteriaValue,
        DateOnly today)
    {
        // §10.1.5: CriteriaValue is a range like "13-35". An
        // unparseable value produces no candidate (B1).
        var parts = criteriaValue.Split('-',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2
            || !int.TryParse(parts[0], out var min)
            || !int.TryParse(parts[1], out var max))
        {
            return false;
        }

        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age)) age--;
        return age >= min && age <= max;
    }

    private static bool EvaluateTenure(
        DateOnly joinDate,
        DateOnly today,
        string criteriaValue)
    {
        // §10.1.5: CriteriaValue is a number of days. An unparseable
        // value produces no candidate (B1).
        if (!int.TryParse(criteriaValue, out var threshold))
        {
            return false;
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
