using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D11_SystemSetting;

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
    private const string TenureThresholdKey = "TenureThresholdDays";
    private const string AgeRangeMinKey = "AgeRangeMin";
    private const string AgeRangeMaxKey = "AgeRangeMax";

    private readonly MknDbContext _db;
    private readonly IDispatchNotificationService _notification;
    private readonly TimeProvider _clock;

    public GenerateAssignmentService(
        MknDbContext db,
        IDispatchNotificationService notification,
        TimeProvider clock)
    {
        _db = db;
        _notification = notification;
        _clock = clock;
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

            // Pre-load system settings used by criteria evaluation.
            var tenureThresholdDays = await ReadIntSettingAsync(TenureThresholdKey, cancellationToken);
            var ageRangeMin = await ReadIntSettingAsync(AgeRangeMinKey, cancellationToken) ?? 0;
            var ageRangeMax = await ReadIntSettingAsync(AgeRangeMaxKey, cancellationToken) ?? 120;

            var timeZone = await TimeZoneResolver.ResolveAsync(_db, cancellationToken);
            var today = TimeZoneResolver.ToDateInTimeZone(_clock.GetUtcNow(), timeZone);

            var totalCreated = 0;
            var fullyFilled = 0;
            var partiallyFilled = 0;
            var unfilled = 0;

            foreach (var occurrence in occurrences)
            {
                var outcome = await FillOccurrenceAsync(
                    occurrence,
                    today,
                    tenureThresholdDays,
                    ageRangeMin,
                    ageRangeMax,
                    cancellationToken);

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
        int AssignmentsCreated);

    private async Task<OccurrenceFillOutcome> FillOccurrenceAsync(
        ServiceOccurrence occurrence,
        DateOnly today,
        int? tenureThresholdDays,
        int ageRangeMin,
        int ageRangeMax,
        CancellationToken cancellationToken)
    {
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
                    tenureThresholdDays,
                    ageRangeMin,
                    ageRangeMax,
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
                    await _notification.DispatchAssignmentNoticeAsync(assignment, cancellationToken);
                }
            }

            dutyOutcomes.Add(new DutyFillOutcome(
                duty.DutyId,
                duty.RequiredSlotCount,
                filledForThisDuty));
        }

        return new OccurrenceFillOutcome(dutyOutcomes, totalCreated);
    }

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

        // If no schedule (event-sourced occurrence), resolve the service
        // definition differently. Event-sourced occurrences inherit from
        // the ProgramItem's linked ServiceDefinition, but the schema
        // does not store ServiceDefId on the occurrence — the link is
        // via the ServiceOccurrenceDuty table for overrides only.
        //
        // For the current implementation, event-sourced occurrences are
        // treated as having no inheritable duty list unless
        // ServiceOccurrenceDuty rows supply one. This is consistent with
        // the specification: 5.0 is defined for schedule-sourced
        // occurrences; event-sourced occurrences are populated through
        // manual assignment.
        if (serviceDefId is null)
        {
            return new List<RequiredDuty>();
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
        int? tenureThresholdDays,
        int ageRangeMin,
        int ageRangeMax,
        CancellationToken cancellationToken)
    {
        // Step 1 — Eligibility gate. A member is a candidate only if
        // they have an applicable Eligibility row for this duty: latest
        // GrantedDate where RevokedDate is null, tiebroken by greatest
        // EligibilityId (§10.1.4).
        var eligibleMemberIds = await ResolveEligibleMembersAsync(dutyId, cancellationToken);

        if (eligibleMemberIds.Count == 0)
        {
            return new List<int>();
        }

        // Step 2 — Load member data for the eligible set.
        var members = await _db.Members
            .Where(m => eligibleMemberIds.Contains(m.MemberId) && m.IsActive)
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
                        tenureThresholdDays,
                        ageRangeMin,
                        ageRangeMax,
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
        CancellationToken cancellationToken)
    {
        // For each member, the applicable Eligibility row is the one
        // with the latest GrantedDate whose RevokedDate is null,
        // tiebroken by the greatest EligibilityId.
        //
        // This is expressed as a single query that selects, per member,
        // the winning row.
        var rows = await _db.Eligibilities
            .Where(e => e.DutyId == dutyId && e.RevokedDate == null)
            .ToListAsync(cancellationToken);

        var eligible = rows
            .GroupBy(e => e.MemberId)
            .Select(g => g
                .OrderByDescending(e => e.GrantedDate)
                .ThenByDescending(e => e.EligibilityId)
                .First())
            .Where(e => e.RevokedDate == null)
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
        int? tenureThresholdDays,
        int ageRangeMin,
        int ageRangeMax,
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
                return EvaluateAgeRange(member.DateOfBirth, criteriaValue, today, ageRangeMin, ageRangeMax);

            case "Tenure":
                return EvaluateTenure(member.JoinDate, today, tenureThresholdDays, criteriaValue);

            case "MembershipStage":
                return string.Equals(member.MembershipStage, criteriaValue, StringComparison.OrdinalIgnoreCase);

            case "BranchAttendanceRecency":
                return await EvaluateBranchAttendanceRecencyAsync(
                    member, criteriaValue, occurrence, branchAttendanceCache, cancellationToken);

            case "EligibilityFlag":
                // The Eligibility Flag criterion references a duty; the
                // member must have a current Eligibility grant for it.
                // The CriteriaValue is the duty name or id — for the
                // current implementation, treat it as a duty id.
                return await EvaluateEligibilityFlagAsync(member, criteriaValue, cancellationToken);

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
        DateOnly today,
        int fallbackMin,
        int fallbackMax)
    {
        // CriteriaValue is a range like "13-35". Fall back to system
        // settings if parsing fails — the settings provide the outer
        // bounds, the value provides the specific range.
        int min, max;

        var parts = criteriaValue.Split('-', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 2
            && int.TryParse(parts[0], out var lo)
            && int.TryParse(parts[1], out var hi))
        {
            min = lo;
            max = hi;
        }
        else
        {
            min = fallbackMin;
            max = fallbackMax;
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
        int? tenureThresholdDays,
        string criteriaValue)
    {
        // CriteriaValue is a number of days. If unparseable, fall back
        // to the system setting.
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

        var days = today.DayNumber - joinDate.DayNumber;
        return days >= threshold;
    }

    private async Task<bool> EvaluateBranchAttendanceRecencyAsync(
        Member member,
        string criteriaValue,
        ServiceOccurrence occurrence,
        Dictionary<int, bool> cache,
        CancellationToken cancellationToken)
    {
        // CriteriaValue is a window in days. Attendance is branch-agnostic
        // for this implementation — the criterion is presence at any
        // occurrence within the window, matching the MKN default.
        if (!int.TryParse(criteriaValue, out var windowDays))
        {
            return false;
        }

        var cutoff = _clock.GetUtcNow().AddDays(-windowDays);

        var hasRecent = await _db.AttendanceRecords
            .AnyAsync(a => a.MemberId == member.MemberId
                && a.Timestamp >= cutoff,
                cancellationToken);

        return hasRecent;
    }

    private async Task<bool> EvaluateEligibilityFlagAsync(
        Member member,
        string criteriaValue,
        CancellationToken cancellationToken)
    {
        // CriteriaValue names a duty (by id or name). We match by id
        // for determinism.
        if (!int.TryParse(criteriaValue, out var dutyId))
        {
            return false;
        }

        var exists = await _db.Eligibilities
            .AnyAsync(e => e.MemberId == member.MemberId
                && e.DutyId == dutyId
                && e.RevokedDate == null,
                cancellationToken);

        return exists;
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
