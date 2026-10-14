using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D11_SystemSetting;
using Microsoft.Extensions.Logging;

namespace MknMs.Application.Processes.Operations.GenerateAssignment;

/// <summary>
/// Implementation of process 5.0 Generate Assignment.
/// </summary>
/// <remarks>
/// Fills the required duty slots of a ServiceOccurrence by producing
/// RosterAssignment rows.
///
/// The two-mechanism model from §5:
///   - Eligibility gates candidacy (binary, admin-granted).
///   - Duty Rule ranks candidates by tier (the first tier producing
///     at least one eligible, available candidate supplies the set).
///
/// The unit of fill is the duty, not the occurrence. Each duty's slots
/// are counted independently: filled, partially filled, or unfilled.
/// The counters in GenerateAssignmentResult reflect this — they count
/// duties, per the invariants in §10.
///
/// Additive — never modifies or removes an existing assignment.
/// Fully re-entrant — an interrupted run resumes on the next run.
/// Invokes 9.0 Dispatch Notification for each new assignment.
///
/// Specification: §5, §10.
/// </remarks>
public sealed class GenerateAssignmentService : IGenerateAssignmentService
{
    private readonly MknDbContext _db;
    private readonly IDispatchNotificationService _notification;
    private readonly TimeProvider _clock;
    private readonly IOccurrenceLockService _occurrenceLock;
    private readonly ILogger<GenerateAssignmentService> _logger;

    public GenerateAssignmentService(
        MknDbContext db,
        IDispatchNotificationService notification,
        TimeProvider clock,
        IOccurrenceLockService occurrenceLock,
        ILogger<GenerateAssignmentService> logger)
    {
        _db = db;
        _notification = notification;
        _clock = clock;
        _occurrenceLock = occurrenceLock;
        _logger = logger;
    }

    public async Task<GenerateAssignmentResult> RunAsync(
        GenerateAssignmentCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            return new GenerateAssignmentResult
            {
                Status = "Failure",
                OccurrencesProcessed = 0,
                AssignmentsCreated = 0,
                DutiesFullyFilled = 0,
                DutiesPartiallyFilled = 0,
                DutiesUnfilled = 0,
                ErrorDetail = "Exactly one of OccurrenceId or (FromDate, ToDate) must be supplied.",
            };
        }

        try
        {
            var occurrences = await ResolveTargetOccurrencesAsync(command, cancellationToken);

            var timeZoneResult = await TimeZoneResolver.ResolveAsync(_db, cancellationToken);
            if (!timeZoneResult.IsSuccess)
            {
                return new GenerateAssignmentResult
                {
                    Status = "Failure",
                    OccurrencesProcessed = 0,
                    AssignmentsCreated = 0,
                    DutiesFullyFilled = 0,
                    DutiesPartiallyFilled = 0,
                    DutiesUnfilled = 0,
                    ErrorDetail = timeZoneResult.ErrorDetail,
                };
            }

            // §15.3.12: AttendanceRegisterEnabled is required for the
            // Branch-Attendance Recency criterion. Absent or invalid
            // -> the whole run fails, per the required-setting rule.
            var registerSetting = await _db.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "AttendanceRegisterEnabled",
                    cancellationToken);

            if (registerSetting?.Value is null)
            {
                return new GenerateAssignmentResult
                {
                    Status = "Failure",
                    OccurrencesProcessed = 0,
                    AssignmentsCreated = 0,
                    DutiesFullyFilled = 0,
                    DutiesPartiallyFilled = 0,
                    DutiesUnfilled = 0,
                    ErrorDetail = "Required setting 'AttendanceRegisterEnabled' is not configured.",
                };
            }

            if (!bool.TryParse(registerSetting.Value, out var globalRegisterEnabled))
            {
                return new GenerateAssignmentResult
                {
                    Status = "Failure",
                    OccurrencesProcessed = 0,
                    AssignmentsCreated = 0,
                    DutiesFullyFilled = 0,
                    DutiesPartiallyFilled = 0,
                    DutiesUnfilled = 0,
                    ErrorDetail = "Setting 'AttendanceRegisterEnabled' must be 'true' or 'false'.",
                };
            }

            var today = TimeZoneResolver.ToDateInTimeZone(_clock.GetUtcNow(), timeZoneResult.Zone!);

            var totalCreated = 0;
            var fullyFilled = 0;
            var partiallyFilled = 0;
            var unfilled = 0;

            foreach (var occurrence in occurrences)
            {
                var outcome = await FillOccurrenceAsync(
                    occurrence,
                    today,
                    globalRegisterEnabled,
                    cancellationToken);

                if (outcome.ContentionDetail is not null)
                {
                    return new GenerateAssignmentResult
                    {
                        Status = "Failure",
                        OccurrencesProcessed = 0,
                        AssignmentsCreated = 0,
                        DutiesFullyFilled = 0,
                        DutiesPartiallyFilled = 0,
                        DutiesUnfilled = 0,
                        ErrorDetail = outcome.ContentionDetail,
                    };
                }

                totalCreated += outcome.AssignmentsCreated;

                // The unit of fill is the duty. Each duty contributes one
                // to exactly one of the three counters. A duty whose
                // RequiredSlots is zero is out of scope and contributes
                // to none.
                foreach (var duty in outcome.Duties)
                {
                    if (duty.RequiredSlots == 0)
                    {
                        continue;
                    }

                    if (duty.FilledSlots == 0)
                    {
                        unfilled++;
                    }
                    else if (duty.FilledSlots >= duty.RequiredSlots)
                    {
                        fullyFilled++;
                    }
                    else
                    {
                        partiallyFilled++;
                    }
                }
            }

            return new GenerateAssignmentResult
            {
                Status = "Success",
                OccurrencesProcessed = occurrences.Count,
                AssignmentsCreated = totalCreated,
                DutiesFullyFilled = fullyFilled,
                DutiesPartiallyFilled = partiallyFilled,
                DutiesUnfilled = unfilled,
            };
        }
        catch (Exception ex)
        {
            return new GenerateAssignmentResult
            {
                Status = "Failure",
                OccurrencesProcessed = 0,
                AssignmentsCreated = 0,
                DutiesFullyFilled = 0,
                DutiesPartiallyFilled = 0,
                DutiesUnfilled = 0,
                ErrorDetail = ex.Message,
            };
        }
    }

    // -----------------------------------------------------------------
    // Target resolution
    // -----------------------------------------------------------------

    private async Task<List<ServiceOccurrence>> ResolveTargetOccurrencesAsync(
        GenerateAssignmentCommand command,
        CancellationToken cancellationToken)
    {
        if (command.IsSingleOccurrence)
        {
            var single = await _db.ServiceOccurrences
                .Where(o => o.OccurrenceId == command.OccurrenceId!.Value)
                .ToListAsync(cancellationToken);

            return single;
        }

        return await _db.ServiceOccurrences
            .Where(o => o.Date >= command.FromDate!.Value
                && o.Date <= command.ToDate!.Value)
            .OrderBy(o => o.Date)
            .ThenBy(o => o.OccurrenceId)
            .ToListAsync(cancellationToken);
    }

    // -----------------------------------------------------------------
    // Filling one occurrence
    // -----------------------------------------------------------------

    private sealed record DutyFillOutcome(int DutyId, int RequiredSlots, int FilledSlots);

    private sealed record OccurrenceFillOutcome(
        IReadOnlyList<DutyFillOutcome> Duties,
        int AssignmentsCreated,
        string? ContentionDetail = null);

    private async Task<OccurrenceFillOutcome> FillOccurrenceAsync(
        ServiceOccurrence occurrence,
        DateOnly today,
        bool globalRegisterEnabled,
        CancellationToken cancellationToken)
    {
        // B6: serialize every writer that can change this occurrence's
        // slot occupancy. 5.0, 7.0's re-resolution, and 12.0 all
        // acquire the same lock. Contention yields a Failure rather
        // than a silent overfill.
        await using var lease = await _occurrenceLock.TryAcquireAsync(
            occurrence.OccurrenceId, cancellationToken);

        if (lease is null)
        {
            return new OccurrenceFillOutcome(
                Array.Empty<DutyFillOutcome>(),
                0,
                $"Another writer is filling occurrence {occurrence.OccurrenceId}.");
        }

        // Effective duty list: inherited from ServiceDefinitionDuty, then
        // adjusted by ServiceOccurrenceDuty overrides (Added / Removed,
        // RequiredSlotCount override).
        var requiredDuties = await ResolveEffectiveDutiesAsync(occurrence, cancellationToken);

        if (requiredDuties.Count == 0)
        {
            return new OccurrenceFillOutcome(Array.Empty<DutyFillOutcome>(), 0);
        }

        // Every row on the occurrence, with duty and terminality. Used
        // for capacity counting (non-terminal only), occurrence
        // availability (non-terminal only), and slot history (any row,
        // terminal or not — §15.2.1 makes a second row impossible).
        var existingRows = await _db.RosterAssignments
            .Where(a => a.OccurrenceId == occurrence.OccurrenceId)
            .Select(a => new
            {
                a.DutyId,
                a.MemberId,
                IsTerminal = a.AssignmentStatusId != null
                    && _db.AssignmentStatuses
                        .Where(s => s.AssignmentStatusId == a.AssignmentStatusId)
                        .Select(s => s.IsTerminal)
                        .FirstOrDefault(),
                IsNull = a.AssignmentStatusId == null,
            })
            .ToListAsync(cancellationToken);

        // Non-terminal rows (including NULL status, which awaits 7.0's
        // initial transition) — the capacity and availability rule
        // (§10.1.2, §10.1.3).
        var nonTerminalRows = existingRows
            .Where(x => x.IsNull || !x.IsTerminal)
            .Select(x => (x.DutyId, x.MemberId))
            .ToList();

        var membersOnOccurrence = nonTerminalRows
            .Select(x => x.MemberId)
            .ToHashSet();

        // Slot history: anyone holding ANY row on a duty's slot. This
        // honours the unique constraint on (MemberId, DutyId,
        // OccurrenceId) (§15.2.1): a member who already holds a row on
        // the slot — even a terminal one — cannot be offered it again.
        var slotHistory = existingRows
            .GroupBy(x => x.DutyId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.MemberId).ToHashSet());

        var dutyOutcomes = new List<DutyFillOutcome>(requiredDuties.Count);
        var totalCreated = 0;

        foreach (var duty in requiredDuties)
        {
            var existingCount = nonTerminalRows.Count(x => x.DutyId == duty.DutyId);
            var filledForThisDuty = existingCount;

            var remaining = duty.RequiredSlotCount - existingCount;
            if (remaining > 0)
            {
                // Exclusion for this duty's candidates: anyone with a
                // live row anywhere on the occurrence (§10.1.2), plus
                // anyone with any row on this duty's slot (§15.2.1).
                var exclusion = new HashSet<int>(membersOnOccurrence);
                if (slotHistory.TryGetValue(duty.DutyId, out var onSlot))
                {
                    exclusion.UnionWith(onSlot);
                }

                var candidates = await FindCandidatesAsync(
                    occurrence,
                    duty.DutyId,
                    duty.RequiredSlotCount,
                    existingCount,
                    exclusion,
                    today,
                    globalRegisterEnabled,
                    cancellationToken);

                var toAssign = candidates.Take(remaining).ToList();

                foreach (var memberId in toAssign)
                {
                    var assignment = new RosterAssignment
                    {
                        MemberId = memberId,
                        DutyId = duty.DutyId,
                        OccurrenceId = occurrence.OccurrenceId,
                        AssignmentStatusId = null,
                        ApprovedBy = null,
                        AssignmentSource = "Automatic",
                        AssignedBy = null,
                        CreatedAt = _clock.GetUtcNow(),
                    };

                    _db.RosterAssignments.Add(assignment);
                    await _db.SaveChangesAsync(cancellationToken);

                    membersOnOccurrence.Add(memberId);
                    filledForThisDuty++;
                    totalCreated++;

                    // Fire-and-forget notification (the 5.0 → 9.0 edge).
                    // §10.1.8: a thrown exception or a timeout from the
                    // invocation does not roll back the assignment and
                    // does not mark the run as failed. 5.0 catches the
                    // failure, logs it, and continues.
                    try
                    {
                        await _notification.DispatchAssignmentNoticeAsync(
                            assignment, cancellationToken);
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(
                            ex,
                            "Notification dispatch failed for assignment {AssignmentId}. "
                            + "The assignment is committed; the run continues.",
                            assignment.AssignmentId);
                    }
                }
            }

            dutyOutcomes.Add(new DutyFillOutcome(
                duty.DutyId,
                duty.RequiredSlotCount,
                filledForThisDuty));
        }

        return new OccurrenceFillOutcome(dutyOutcomes, totalCreated);
    }

    // -----------------------------------------------------------------
    // Effective duty list
    // -----------------------------------------------------------------

    private sealed record RequiredDuty(int DutyId, int RequiredSlotCount);

    private async Task<List<RequiredDuty>> ResolveEffectiveDutiesAsync(
        ServiceOccurrence occurrence,
        CancellationToken cancellationToken)
    {
        var schedule = occurrence.ScheduleId is int scheduleId
            ? await _db.ServiceSchedules
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, cancellationToken)
            : null;

        int? serviceDefId = schedule?.ServiceDefId;

        // If no schedule, this is an event-sourced occurrence. There is
        // no inheritable duty list: the ServiceSchedule that would
        // supply one is absent. Only ServiceOccurrenceDuty rows with
        // Action = Added supply duties. Removed has no meaning here,
        // because there is nothing to remove.
        if (serviceDefId is null)
        {
            // Event-sourced occurrence. §10.1.8 / acceptance matrix:
            // no duty list is inherited from a schedule; only
            // ServiceOccurrenceDuty rows with Action = Added supply
            // duties. Removed has no meaning here because there is
            // nothing to remove.
            var addedOverrides = await _db.ServiceOccurrenceDuties
                .Where(d => d.OccurrenceId == occurrence.OccurrenceId
                    && d.Action == "Added")
                .ToListAsync(cancellationToken);

            return addedOverrides
                .Select(ov => new RequiredDuty(
                    ov.DutyId,
                    ov.RequiredSlotCount ?? 1))
                .ToList();
        }

        var inherited = await _db.ServiceDefinitionDuties
            .Where(d => d.ServiceDefId == serviceDefId.Value && d.IsActive)
            .Select(d => new RequiredDuty(d.DutyId, d.RequiredSlotCount))
            .ToListAsync(cancellationToken);

        var overrides = await _db.ServiceOccurrenceDuties
            .Where(d => d.OccurrenceId == occurrence.OccurrenceId)
            .ToListAsync(cancellationToken);

        if (overrides.Count == 0)
        {
            return inherited;
        }

        var byDuty = inherited.ToDictionary(d => d.DutyId);

        foreach (var ov in overrides)
        {
            if (ov.Action == "Removed")
            {
                byDuty.Remove(ov.DutyId);
                continue;
            }

            if (ov.Action == "Added")
            {
                var slotCount = ov.RequiredSlotCount
                    ?? (byDuty.TryGetValue(ov.DutyId, out var existing)
                        ? existing.RequiredSlotCount
                        : 1);

                byDuty[ov.DutyId] = new RequiredDuty(ov.DutyId, slotCount);
                continue;
            }

            // Unknown action — skip. The CHECK constraint on the table
            // prevents this from existing in practice.
        }

        return byDuty.Values.ToList();
    }

    // -----------------------------------------------------------------
    // Candidate selection
    // -----------------------------------------------------------------

    private async Task<List<int>> FindCandidatesAsync(
        ServiceOccurrence occurrence,
        int dutyId,
        int requiredSlotCount,
        int existingCount,
        HashSet<int> membersOnOccurrence,
        DateOnly today,
        bool globalRegisterEnabled,
        CancellationToken cancellationToken)
    {
        // Step 1 — Eligibility gate. A member is a candidate only if
        // they have an applicable Eligibility row for this duty: the
        // row with the latest GrantedDate that is in the past and whose
        // RevokedDate is either null or in the future (§10.1.4).
        var eligibleMemberIds = await ResolveEligibleMembersAsync(
            dutyId, today, cancellationToken);

        if (eligibleMemberIds.Count == 0)
        {
            return new List<int>();
        }

        // Step 2 — Load member data for the eligible set.
        var members = await _db.Members
            .Include(m => m.MemberStatus)
            .Where(m => eligibleMemberIds.Contains(m.MemberId)
                && m.MemberStatus.IsRosterable
                && !m.MemberStatus.IsDeleted)
            .ToListAsync(cancellationToken);

        // Step 3 — Availability: exclude members with a non-terminal
        // assignment on this occurrence (§10.1.2).
        members = members
            .Where(m => !membersOnOccurrence.Contains(m.MemberId))
            .ToList();

        if (members.Count == 0)
        {
            return new List<int>();
        }

        // Step 4 — Duty Rule ranking. If no rules exist, every eligible
        // member is an equal candidate (§5, §10.1.5).
        var rules = await _db.DutyRules
            .Where(r => r.DutyId == dutyId && r.IsActive)
            .OrderBy(r => r.TierOrder)
            .ThenBy(r => r.RuleId)
            .ToListAsync(cancellationToken);

        if (rules.Count == 0)
        {
            return members.Select(m => m.MemberId).OrderBy(id => id).ToList();
        }

        // Group rules by tier.
        var tiers = rules
            .GroupBy(r => r.TierOrder)
            .OrderBy(g => g.Key)
            .ToList();

        // Pre-load attendance data lazily only if needed.
        var branchAttendanceCache = new Dictionary<int, bool>();

        foreach (var tier in tiers)
        {
            var tierMembers = new List<int>();

            foreach (var member in members)
            {
                var matchesTier = true;

                foreach (var rule in tier)
                {
                    var matches = await EvaluateCriterionAsync(
                        member,
                        rule.CriteriaType,
                        rule.CriteriaValue,
                        occurrence,
                        today,
                        globalRegisterEnabled,
                        branchAttendanceCache,
                        cancellationToken);

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
                // First tier producing at least one candidate supplies
                // the candidate set. Deterministic order: ascending
                // MemberId. No fairness guarantee (§10.1.6).
                return tierMembers.OrderBy(id => id).ToList();
            }
        }

        // No tier produced a candidate.
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
        // The applicable row per (member, duty) is the one with the
        // latest GrantedDate among those, tiebroken by the greatest
        // EligibilityId.
        var rows = await _db.Eligibilities
            .Where(e => e.DutyId == dutyId
                && e.GrantedDate <= today
                && (e.RevokedDate == null || e.RevokedDate > today))
            .ToListAsync(cancellationToken);

        var eligible = rows
            .GroupBy(e => e.MemberId)
            .Select(g => g
                .OrderByDescending(e => e.GrantedDate)
                .ThenByDescending(e => e.EligibilityId)
                .First())
            .Select(e => e.MemberId)
            .ToHashSet();

        return eligible;
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
        bool globalRegisterEnabled,
        Dictionary<int, bool> branchAttendanceCache,
        CancellationToken cancellationToken)
    {
        switch (criteriaType)
        {
            case "Role":
                return await EvaluateRoleAsync(member, criteriaValue, cancellationToken);

            case "Gender":
                return string.Equals(member.Gender, criteriaValue, StringComparison.OrdinalIgnoreCase);

            case "AgeRange":
                return EvaluateAgeRange(member.DateOfBirth, criteriaValue, today);

            case "Tenure":
                return EvaluateTenure(member.JoinDate, today, criteriaValue);

            case "MembershipStage":
                return string.Equals(member.MembershipStage, criteriaValue, StringComparison.OrdinalIgnoreCase);

            case "BranchAttendanceRecency":
                // §10.1.5: the criterion is "Member has an
                // AttendanceRecord at the specified branch within the
                // configured window." The specified branch is the
                // branch of the occurrence being filled; the window is
                // the rule's CriteriaValue, in days.
                return await EvaluateBranchAttendanceRecencyAsync(
                    member, criteriaValue, occurrence, globalRegisterEnabled,
                    cancellationToken);

            case "EligibilityFlag":
                // The Eligibility Flag criterion references a duty; the
                // member must hold a current Eligibility grant for it.
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
                // Reserved criteria types — not evaluated until a
                // future specification revision activates them. A tier
                // consisting entirely of reserved criteria produces no
                // candidates (§10.1.5).
                return false;

            default:
                return false;
        }
    }

    private async Task<bool> EvaluateRoleAsync(
        Member member,
        string criteriaValue,
        CancellationToken cancellationToken)
    {
        // CriteriaValue names a role. Match by name, case-insensitive.
        var role = await _db.Roles
            .FirstOrDefaultAsync(r => r.Name == criteriaValue, cancellationToken);

        if (role is null)
        {
            return false;
        }

        return member.RoleId == role.RoleId;
    }

    private static bool EvaluateAgeRange(
        DateOnly dateOfBirth,
        string criteriaValue,
        DateOnly today)
    {
        // §10.1.5: CriteriaValue is a range like "13-35". An
        // unparseable value produces no candidate (B1) — there is no
        // fallback to a system setting; the criterion value is the
        // sole source.
        var parts = criteriaValue.Split('-',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2
            || !int.TryParse(parts[0], out var min)
            || !int.TryParse(parts[1], out var max))
        {
            return false;
        }

        var age = today.Year - dateOfBirth.Year;
        if (dateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age >= min && age <= max;
    }

    private static bool EvaluateTenure(
        DateOnly joinDate,
        DateOnly today,
        string criteriaValue)
    {
        // §10.1.5: CriteriaValue is a number of days. An unparseable
        // value produces no candidate (B1) — there is no fallback to a
        // system setting; the criterion value is the sole source.
        if (!int.TryParse(criteriaValue, out var threshold))
        {
            return false;
        }

        var days = today.DayNumber - joinDate.DayNumber;
        return days >= threshold;
    }

    private async Task<bool> EvaluateMemberAttributeAsync(
        Member member,
        string criteriaValue,
        CancellationToken cancellationToken)
    {
        // §10.1.5: CriteriaValue is "AttributeTypeName=RequiredValue".
        // The type is resolved by exact case-sensitive name; the
        // RequiredValue may contain spaces. An unparseable value or an
        // unknown type produces no candidate (B1).
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

        var match = await _db.MemberAttributeValues
            .AnyAsync(v => v.MemberId == member.MemberId
                && v.AttributeTypeId == attrTypeId.Value
                && v.Value == requiredValue,
                cancellationToken);

        return match;
    }

    private static bool EvaluateMemberStatus(Member member, string criteriaValue)
    {
        // §10.1.5: CriteriaValue is a comma-separated list of positive
        // integers (MemberStatusIDs), no whitespace. The member's
        // current MemberStatusId must be one of them. An unparseable
        // value produces no candidate (B1).
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
        // §10.1.5: Youth is computed from DateOfBirth against the
        // configured YouthAgeMin and YouthAgeMax. Both settings are
        // required; when either is absent the criterion cannot be
        // evaluated and matches no candidate.
        var minSetting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "YouthAgeMin", cancellationToken);
        var maxSetting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == "YouthAgeMax", cancellationToken);

        if (minSetting?.Value is null || maxSetting?.Value is null
            || !int.TryParse(minSetting.Value, out var min)
            || !int.TryParse(maxSetting.Value, out var max))
        {
            return false;
        }

        var age = today.Year - member.DateOfBirth.Year;
        if (member.DateOfBirth > today.AddYears(-age))
        {
            age--;
        }

        return age >= min && age <= max;
    }

    private async Task<bool> EvaluateBranchAttendanceRecencyAsync(
        Member member,
        string criteriaValue,
        ServiceOccurrence occurrence,
        bool globalRegisterEnabled,
        CancellationToken cancellationToken)
    {
        // §10.1.5: the criterion is skipped for occurrences whose
        // effective attendance register scope is off. §9.6 defines
        // the resolution: branch override for schedule-sourced
        // occurrences, event override for event-sourced ones, then
        // the global setting. A skipped criterion contributes no
        // filter; the tier evaluates on its other criteria, or
        // matches every eligible member if it had none.
        var effectiveScope = await ResolveEffectiveRegisterScopeAsync(
            occurrence, globalRegisterEnabled, cancellationToken);

        if (!effectiveScope)
        {
            return true;
        }

        if (!int.TryParse(criteriaValue, out var windowDays))
        {
            return false;
        }

        // The occurrence's branch. Event-sourced occurrences (no
        // schedule) have no branch context and cannot satisfy the
        // criterion.
        var occurrenceBranchId = await _db.ServiceSchedules
            .Where(s => s.ScheduleId == occurrence.ScheduleId)
            .Select(s => (int?)s.TimeSlot.BranchId)
            .FirstOrDefaultAsync(cancellationToken);

        if (occurrenceBranchId is null)
        {
            return false;
        }

        var cutoff = _clock.GetUtcNow().AddDays(-windowDays);

        // Attendance records whose own occurrence is at the same branch
        // as the occurrence being filled, within the window.
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


    private async Task<bool> ResolveEffectiveRegisterScopeAsync(
        ServiceOccurrence occurrence,
        bool globalRegisterEnabled,
        CancellationToken cancellationToken)
    {
        if (occurrence.ScheduleId is int scheduleId)
        {
            var branchOverride = await _db.ServiceSchedules
                .Where(s => s.ScheduleId == scheduleId)
                .Select(s => s.TimeSlot.Branch.UsesAttendanceRegister)
                .FirstOrDefaultAsync(cancellationToken);

            return branchOverride ?? globalRegisterEnabled;
        }

        if (occurrence.EventId is int eventId)
        {
            var eventOverride = await _db.Events
                .Where(e => e.EventId == eventId)
                .Select(e => e.UsesAttendanceRegister)
                .FirstOrDefaultAsync(cancellationToken);

            return eventOverride ?? globalRegisterEnabled;
        }

        return globalRegisterEnabled;
    }

    private async Task<bool> EvaluateEligibilityFlagAsync(
        Member member,
        string criteriaValue,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // CriteriaValue names a duty (by id or name). We match by id
        // for determinism.
        if (!int.TryParse(criteriaValue, out var dutyId))
        {
            return false;
        }

        // §10.1.4: the grant must be in effect today — GrantedDate not
        // in the future, RevokedDate either absent or in the future.
        var exists = await _db.Eligibilities
            .AnyAsync(e => e.MemberId == member.MemberId
                && e.DutyId == dutyId
                && e.GrantedDate <= today
                && (e.RevokedDate == null || e.RevokedDate > today),
                cancellationToken);

        return exists;
    }
}
