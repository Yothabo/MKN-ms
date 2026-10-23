using MknMs.Application.Processes.Operations.DispatchNotification;

namespace MknMs.Application.Processes.Operations.AttendanceRuleEngine;

/// <summary>
/// Implementation of process 13.0 Attendance Rule Engine.
/// </summary>
/// <remarks>
/// Sequential stateful evaluation. A scheduled run evaluates every
/// enabled rule whose TriggerType is not Manual, in ascending
/// AttendanceRuleId order. Each rule's outcome is persisted before
/// the next rule is evaluated, so a SetStatus outcome applied by an
/// earlier rule is visible to a later rule's trigger and scope.
///
/// The engine is deterministic for a given set of persisted inputs
/// but is not idempotent with respect to its external outcomes. A
/// second run while a trigger still holds may apply the outcome
/// again. No per-rule execution history is maintained.
///
/// Absence window is [today - N, today], inclusive at both
/// boundaries, measured in calendar days in the configured
/// ApplicationTimeZone. The trigger fires when the member has no
/// AttendanceRecord whose related ServiceOccurrence.Date falls in
/// the window. The attendance timestamp is not used.
///
/// Scope resolution is per member: the member's home branch override
/// (Branch.UsesAttendanceRegister), then the global
/// AttendanceRegisterEnabled. Event overrides are not consulted.
/// When the effective scope is off, absence-based triggers do not
/// fire for that member; ReadmissionCount triggers apply regardless.
///
/// Invokes 9.0 only when an Attendance Rule's OutcomeType is Notify.
/// That is the process's only invocation edge (§16.2 edge I4).
///
/// Specification: §13.6, §9.6, §16; docs/decisions.md B8, B10, B11,
/// B12, B13, B14.
/// </remarks>
public sealed class AttendanceRuleEngineService : IAttendanceRuleEngineService
{
    private const string AttendanceRegisterEnabledKey = "AttendanceRegisterEnabled";

    private readonly MknDbContext _db;
    private readonly IDispatchNotificationService _notification;
    private readonly TimeProvider _clock;

    public AttendanceRuleEngineService(
        MknDbContext db,
        IDispatchNotificationService notification,
        TimeProvider clock)
    {
        _db = db;
        _notification = notification;
        _clock = clock;
    }

    public async Task<AttendanceRuleEngineResult> RunAsync(
        AttendanceRuleEngineCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            return Failure(
                ConfigurationErrorCodes.InvalidValue,
                "Command must be a Scheduled run with no RuleId and no "
                + "ActingAdminId, or a Manual invocation with a positive "
                + "RuleId and a positive ActingAdminId.");
        }

        // §13.6: the manual invocation carries the ActingAdminID of
        // the administrator performing it, which resolves to an Admin
        // row whose IsActive = true. A positive ID alone does not
        // satisfy the requirement; the id must name a real, active
        // administrator.
        if (command.IsManual)
        {
            var actingAdmin = await _db.Admins
                .FirstOrDefaultAsync(a => a.AdminId == command.ActingAdminId!.Value,
                    cancellationToken);

            if (actingAdmin is null)
            {
                return Failure(
                    ConfigurationErrorCodes.NotFound,
                    $"Admin {command.ActingAdminId} does not exist.");
            }

            if (!actingAdmin.IsActive)
            {
                return Failure(
                    ConfigurationErrorCodes.NotFound,
                    $"Admin {command.ActingAdminId} is not active.");
            }
        }

        // §15.3.12: AttendanceRegisterEnabled is required. If it
        // is absent, the engine refuses to run entirely.
        var globalSetting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == AttendanceRegisterEnabledKey,
                cancellationToken);

        if (globalSetting?.Value is null)
        {
            return Failure(
                ConfigurationErrorCodes.ConfigurationMissing,
                $"Required setting '{AttendanceRegisterEnabledKey}' is not configured.");
        }

        if (!bool.TryParse(globalSetting.Value, out var globalRegisterEnabled))
        {
            return Failure(
                ConfigurationErrorCodes.ConfigurationInvalid,
                $"Setting '{AttendanceRegisterEnabledKey}' must be 'true' or 'false'.");
        }

        // Timezone: required. Absent or unrecognised → refuse.
        var tzResult = await TimeZoneResolver.ResolveAsync(_db, cancellationToken);
        if (!tzResult.IsSuccess)
        {
            return Failure(tzResult.ErrorCode!, tzResult.ErrorDetail!);
        }

        var today = TimeZoneResolver.ToDateInTimeZone(_clock.GetUtcNow(), tzResult.Zone!);

        // Load the rules to evaluate.
        var rules = await ResolveRulesAsync(command, cancellationToken);
        if (rules is null)
        {
            return Failure(
                ConfigurationErrorCodes.NotFound,
                $"Attendance Rule {command.RuleId} does not exist, "
                + "is not enabled, or is not a Manual rule.");
        }

        var rulesEvaluated = 0;
        var membersEvaluated = 0;
        var statusChangesApplied = 0;
        var notificationsDispatched = 0;

        foreach (var rule in rules)
        {
            var scopeRows = await _db.AttendanceRuleScopes
                .Where(s => s.AttendanceRuleId == rule.AttendanceRuleId)
                .ToListAsync(cancellationToken);

            var members = await ResolveScopedMembersAsync(scopeRows, cancellationToken);

            foreach (var member in members)
            {
                membersEvaluated++;

                var effectiveScope = await ResolveEffectiveRegisterScopeAsync(
                    member, globalRegisterEnabled, cancellationToken);

                // AbsenceDays is skipped when the register is off
                // at the member's effective scope. ReadmissionCount
                // applies regardless.
                var absenceExempt = !effectiveScope
                    && rule.TriggerType == "AbsenceDays";

                if (absenceExempt)
                {
                    continue;
                }

                var fires = await EvaluateTriggerAsync(
                    rule, member, today, cancellationToken);

                if (!fires)
                {
                    continue;
                }

                var applied = await ApplyOutcomeAsync(rule, member, cancellationToken);

                switch (applied)
                {
                    case OutcomeApplied.StatusChange:
                        statusChangesApplied++;
                        break;
                    case OutcomeApplied.Notification:
                        notificationsDispatched++;
                        break;
                    case OutcomeApplied.NoOp:
                        break;
                }
            }

            rulesEvaluated++;
        }

        return new AttendanceRuleEngineResult
        {
            Status = "Success",
            RulesEvaluated = rulesEvaluated,
            MembersEvaluated = membersEvaluated,
            StatusChangesApplied = statusChangesApplied,
            NotificationsDispatched = notificationsDispatched,
        };
    }

    // -----------------------------------------------------------------
    // Rule resolution
    // -----------------------------------------------------------------

    /// <summary>
    /// Returns the rules to evaluate, or null if the command named a
    /// rule that cannot be evaluated (manual invocation of a missing
    /// or disabled or non-Manual rule).
    /// </summary>
    private async Task<List<AttendanceRule>?> ResolveRulesAsync(
        AttendanceRuleEngineCommand command,
        CancellationToken cancellationToken)
    {
        if (command.IsManual)
        {
            var rule = await _db.AttendanceRules
                .FirstOrDefaultAsync(r => r.AttendanceRuleId == command.RuleId!.Value,
                    cancellationToken);

            if (rule is null
                || !rule.Enabled
                || !rule.IsActive
                || rule.IsDeleted
                || rule.TriggerType != "Manual")
            {
                return null;
            }

            return new List<AttendanceRule> { rule };
        }

        // Scheduled run: every enabled, non-Manual rule in ascending
        // AttendanceRuleId order. Disabled rules are not evaluated.
        var scheduled = await _db.AttendanceRules
            .Where(r => r.Enabled && r.IsActive && !r.IsDeleted
                && r.TriggerType != "Manual")
            .OrderBy(r => r.AttendanceRuleId)
            .ToListAsync(cancellationToken);

        return scheduled;
    }

    // -----------------------------------------------------------------
    // Scope resolution (§13.6)
    // -----------------------------------------------------------------

    /// <summary>
    /// Resolve the rule's scope criteria against the member
    /// population. Scope rows are ANDed. A rule with no scope rows
    /// applies to every member.
    /// </summary>
    private async Task<List<Member>> ResolveScopedMembersAsync(
        List<AttendanceRuleScope> scopeRows,
        CancellationToken cancellationToken)
    {
        // Load every active member. The scope filter is applied in
        // memory using the same criterion evaluators the process uses
        // for triggers where practical; for this phase the scope
        // vocabulary is small and the member population is small.
        var members = await _db.Members
            .Where(m => m.MemberStatus.IsActive && !m.MemberStatus.IsDeleted)
            .ToListAsync(cancellationToken);

        if (scopeRows.Count == 0)
        {
            return members;
        }

        // For each scope row, apply the criterion. A member matches
        // the rule only if every scope row matches. The scope
        // vocabulary is the Duty Rule criteria vocabulary (§13.6).
        var matched = new List<Member>();

        foreach (var member in members)
        {
            var matchesAll = true;

            foreach (var scope in scopeRows)
            {
                var matches = await EvaluateScopeCriterionAsync(
                    scope.CriteriaType, scope.CriteriaValue, member, cancellationToken);

                if (!matches)
                {
                    matchesAll = false;
                    break;
                }
            }

            if (matchesAll)
            {
                matched.Add(member);
            }
        }

        return matched;
    }

    private async Task<bool> EvaluateScopeCriterionAsync(
        string criteriaType,
        string criteriaValue,
        Member member,
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

            case "MembershipStage":
                return string.Equals(member.MembershipStage, criteriaValue, StringComparison.OrdinalIgnoreCase);

            case "MemberStatus":
                // comma-separated MemberStatusIDs, no whitespace.
                if (criteriaValue.Any(char.IsWhiteSpace))
                {
                    return false;
                }
                foreach (var p in criteriaValue.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(p, out var id) && id > 0 && member.MemberStatusId == id)
                    {
                        return true;
                    }
                }
                return false;

            case "AgeRange":
                return EvaluateAgeRange(member.DateOfBirth, criteriaValue,
                    TimeZoneResolver.ToDateInTimeZone(
                        _clock.GetUtcNow(),
                        (await TimeZoneResolver.ResolveAsync(_db, cancellationToken)).Zone!));

            case "Tenure":
                return EvaluateTenure(member.JoinDate,
                    TimeZoneResolver.ToDateInTimeZone(
                        _clock.GetUtcNow(),
                        (await TimeZoneResolver.ResolveAsync(_db, cancellationToken)).Zone!),
                    criteriaValue);

            // Other criteria types are not applicable to scope
            // evaluation for 13.0 at this phase — BranchAttendanceRecency,
            // EligibilityFlag, MemberAttribute, Youth. §13.6 says the
            // scope uses the same vocabulary as Duty Rule; the engine
            // evaluates the subset that is meaningful without an
            // occurrence context. A scope row whose criterion cannot
            // be evaluated does not match.
            default:
                return false;
        }
    }

    // -----------------------------------------------------------------
    // Register scope (§9.6)
    // -----------------------------------------------------------------

    private async Task<bool> ResolveEffectiveRegisterScopeAsync(
        Member member,
        bool globalRegisterEnabled,
        CancellationToken cancellationToken)
    {
        // §9.6: the member's home branch override, then the global
        // setting. Event overrides are not consulted by 13.0.
        var branchOverride = await _db.Branches
            .Where(b => b.BranchId == member.BranchId)
            .Select(b => b.UsesAttendanceRegister)
            .FirstOrDefaultAsync(cancellationToken);

        return branchOverride ?? globalRegisterEnabled;
    }

    // -----------------------------------------------------------------
    // Trigger evaluation
    // -----------------------------------------------------------------

    private async Task<bool> EvaluateTriggerAsync(
        AttendanceRule rule,
        Member member,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        switch (rule.TriggerType)
        {
            case "AbsenceDays":
                return await EvaluateAbsenceAsync(rule, member, today, cancellationToken);

            case "ReadmissionCount":
                return await EvaluateReadmissionCountAsync(rule, member, cancellationToken);

            case "Manual":
                // Manual rules fire unconditionally when invoked.
                return true;

            default:
                return false;
        }
    }

    private async Task<bool> EvaluateAbsenceAsync(
        AttendanceRule rule,
        Member member,
        DateOnly today,
        CancellationToken cancellationToken)
    {
        // §13.6: the window is [today - N, today], inclusive.
        if (rule.TriggerValue is null || !int.TryParse(rule.TriggerValue, out var n) || n < 0)
        {
            return false;
        }

        var from = today.AddDays(-n);

        // The trigger fires when the member has no AttendanceRecord
        // whose related ServiceOccurrence.Date falls within the
        // window. The attendance timestamp is not used.
        var hasAttendance = await _db.AttendanceRecords
            .Where(a => a.MemberId == member.MemberId)
            .Join(_db.ServiceOccurrences,
                a => a.OccurrenceId,
                o => o.OccurrenceId,
                (a, o) => o.Date)
            .AnyAsync(d => d >= from && d <= today, cancellationToken);

        return !hasAttendance;
    }

    private async Task<bool> EvaluateReadmissionCountAsync(
        AttendanceRule rule,
        Member member,
        CancellationToken cancellationToken)
    {
        if (rule.TriggerValue is null || !int.TryParse(rule.TriggerValue, out var n) || n < 0)
        {
            return false;
        }

        var count = await _db.Readmissions
            .CountAsync(r => r.MemberId == member.MemberId, cancellationToken);

        return count >= n;
    }

    // -----------------------------------------------------------------
    // Outcome application
    // -----------------------------------------------------------------

    private enum OutcomeApplied { StatusChange, Notification, NoOp }

    private async Task<OutcomeApplied> ApplyOutcomeAsync(
        AttendanceRule rule,
        Member member,
        CancellationToken cancellationToken)
    {
        switch (rule.OutcomeType)
        {
            case "SetStatus":
                if (rule.OutcomeStatusId is int statusId)
                {
                    // The rule's OutcomeStatusId was validated at write
                    // time, but the status may have been soft-deleted
                    // since. Writing a soft-deleted status would violate
                    // §9.7's "hidden from ordinary views" rule through a
                    // back door. Skip the outcome rather than fail the
                    // run — one misconfigured rule should not stop the
                    // engine evaluating the rest.
                    var statusExists = await _db.MemberStatuses
                        .AnyAsync(s => s.MemberStatusId == statusId && !s.IsDeleted,
                            cancellationToken);
                    if (!statusExists)
                    {
                        return OutcomeApplied.NoOp;
                    }

                    member.MemberStatusId = statusId;
                    await _db.SaveChangesAsync(cancellationToken);
                    return OutcomeApplied.StatusChange;
                }
                return OutcomeApplied.NoOp;

            case "Notify":
                // Fire-and-forget. A failure does not roll back or
                // fail the run. Matches 5.0 / 7.0 / 12.0 shape: the
                // 9.0 invocation is wrapped so a transport fault
                // (e.g. missing required NotificationChannel) does
                // not escape mid-run after earlier SetStatus
                // outcomes have committed.
                try
                {
                    await _notification.DispatchAttendanceRuleNoticeAsync(
                        member, rule, cancellationToken);
                }
                catch (Exception)
                {
                    // Swallow: the run continues; another rule may
                    // still apply; a future sweep retries any Notify
                    // rule whose trigger still holds.
                }
                return OutcomeApplied.Notification;

            case "NoOp":
            default:
                return OutcomeApplied.NoOp;
        }
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static bool EvaluateAgeRange(
        DateOnly dateOfBirth,
        string criteriaValue,
        DateOnly today)
    {
        var parts = criteriaValue.Split('-',
            StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length != 2
            || !int.TryParse(parts[0], out var min)
            || !int.TryParse(parts[1], out var max))
        {
            return false;
        }

        var age = MemberAge.CompletedYears(dateOfBirth, today);
        return age >= min && age <= max;
    }

    private static bool EvaluateTenure(
        DateOnly joinDate,
        DateOnly today,
        string criteriaValue)
    {
        if (!int.TryParse(criteriaValue, out var threshold))
        {
            return false;
        }

        return MemberTenure.DaysSince(joinDate, today) >= threshold;
    }

    private static AttendanceRuleEngineResult Failure(string code, string detail) =>
        new()
        {
            Status = "Failure",
            RulesEvaluated = 0,
            MembersEvaluated = 0,
            StatusChangesApplied = 0,
            NotificationsDispatched = 0,
            ErrorCode = code,
            ErrorDetail = detail,
        };
}
