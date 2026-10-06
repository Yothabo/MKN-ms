namespace MknMs.Application.Processes.Operations.EvaluateFillStatus;

/// <summary>
/// Implementation of process 10.0 Evaluate Fill Status.
/// </summary>
/// <remarks>
/// Mechanical classification of each occurrence's fill state against
/// the three required mapping settings. The classification is fixed:
/// Unfilled, Partially Filled, or Filled. The vocabulary is
/// administrator-defined. The mapping between them is config, via
/// SystemSetting.
///
/// The service refuses to run entirely if any of the three required
/// settings is absent. It does not partially evaluate occurrences.
///
/// Writes only ServiceOccurrence.FillStatusId. No other field on any
/// occurrence is touched. No other store is written. No process is
/// invoked.
///
/// Idempotent: re-running with unchanged assignments produces the same
/// FillStatusId.
///
/// Specification: §12.
/// </remarks>
public sealed class EvaluateFillStatusService : IEvaluateFillStatusService
{
    private const string UnfilledKey = "OutcomeStateUnfilledID";
    private const string PartiallyFilledKey = "OutcomeStatePartiallyFilledID";
    private const string FilledKey = "OutcomeStateFilledID";
    private const string CancelledKey = "OutcomeStateCancelledID";

    private readonly MknDbContext _db;

    public EvaluateFillStatusService(MknDbContext db)
    {
        _db = db;
    }

    public async Task<EvaluateFillStatusResult> RunAsync(
        EvaluateFillStatusCommand command,
        CancellationToken cancellationToken = default)
    {
        if (!command.IsValid)
        {
            return new EvaluateFillStatusResult
            {
                Status = "Failure",
                OccurrencesEvaluated = 0,
                UnfilledCount = 0,
                PartiallyFilledCount = 0,
                FilledCount = 0,
                SkippedCancelledCount = 0,
                ErrorDetail = "Exactly one of OccurrenceId or (FromDate, ToDate) must be supplied.",
            };
        }

        try
        {
            // Load and validate the three required mapping settings and
            // the optional cancellation setting. §12.1.3: any of the
            // three required absent or invalid → refuse to run entirely.
            var unfilledId = await ReadRequiredOutcomeStateIdAsync(UnfilledKey, cancellationToken);
            var partiallyFilledId = await ReadRequiredOutcomeStateIdAsync(PartiallyFilledKey, cancellationToken);
            var filledId = await ReadRequiredOutcomeStateIdAsync(FilledKey, cancellationToken);

            if (unfilledId is null || partiallyFilledId is null || filledId is null)
            {
                return new EvaluateFillStatusResult
                {
                    Status = "Failure",
                    OccurrencesEvaluated = 0,
                    UnfilledCount = 0,
                    PartiallyFilledCount = 0,
                    FilledCount = 0,
                    SkippedCancelledCount = 0,
                    ErrorDetail = $"One or more required settings are not configured: " +
                                  $"{UnfilledKey}, {PartiallyFilledKey}, {FilledKey}.",
                };
            }

            // Optional cancellation setting. §12.1.4. If absent, no
            // exclusion applies. If present, occurrences whose current
            // FillStatusId equals it are skipped.
            var cancelledId = await ReadOptionalOutcomeStateIdAsync(CancelledKey, cancellationToken);

            var occurrences = await ResolveTargetOccurrencesAsync(command, cancellationToken);

            var evaluated = 0;
            var unfilled = 0;
            var partially = 0;
            var filled = 0;
            var skipped = 0;

            foreach (var occurrence in occurrences)
            {
                if (cancelledId is int cancelled && occurrence.FillStatusId == cancelled)
                {
                    skipped++;
                    continue;
                }

                var classification = await ClassifyAsync(occurrence, cancellationToken);

                var targetId = classification switch
                {
                    FillClassification.Unfilled => unfilledId.Value,
                    FillClassification.PartiallyFilled => partiallyFilledId.Value,
                    FillClassification.Filled => filledId.Value,
                    _ => throw new InvalidOperationException(
                        $"Unknown classification {classification}.")
                };

                occurrence.FillStatusId = targetId;
                await _db.SaveChangesAsync(cancellationToken);

                evaluated++;
                switch (classification)
                {
                    case FillClassification.Unfilled: unfilled++; break;
                    case FillClassification.PartiallyFilled: partially++; break;
                    case FillClassification.Filled: filled++; break;
                }
            }

            return new EvaluateFillStatusResult
            {
                Status = "Success",
                OccurrencesEvaluated = evaluated,
                UnfilledCount = unfilled,
                PartiallyFilledCount = partially,
                FilledCount = filled,
                SkippedCancelledCount = skipped,
            };
        }
        catch (Exception ex)
        {
            return new EvaluateFillStatusResult
            {
                Status = "Failure",
                OccurrencesEvaluated = 0,
                UnfilledCount = 0,
                PartiallyFilledCount = 0,
                FilledCount = 0,
                SkippedCancelledCount = 0,
                ErrorDetail = ex.Message,
            };
        }
    }

    // -----------------------------------------------------------------
    // Target resolution
    // -----------------------------------------------------------------

    private async Task<List<ServiceOccurrence>> ResolveTargetOccurrencesAsync(
        EvaluateFillStatusCommand command,
        CancellationToken cancellationToken)
    {
        if (command.IsSingle)
        {
            return await _db.ServiceOccurrences
                .Where(o => o.OccurrenceId == command.OccurrenceId!.Value)
                .ToListAsync(cancellationToken);
        }

        return await _db.ServiceOccurrences
            .Where(o => o.Date >= command.FromDate!.Value
                && o.Date <= command.ToDate!.Value)
            .OrderBy(o => o.Date)
            .ThenBy(o => o.OccurrenceId)
            .ToListAsync(cancellationToken);
    }

    // -----------------------------------------------------------------
    // Classification
    // -----------------------------------------------------------------

    private enum FillClassification { Unfilled, PartiallyFilled, Filled }

    private async Task<FillClassification> ClassifyAsync(
        ServiceOccurrence occurrence,
        CancellationToken cancellationToken)
    {
        // Effective required count. Sum of RequiredSlotCount over the
        // effective duty list, after ServiceOccurrenceDuty overrides.
        var effectiveRequired = await ResolveEffectiveRequiredCountAsync(occurrence, cancellationToken);

        // Active assignments across all duties of this occurrence.
        // Active = non-null status and IsTerminal = false.
        var activeCount = await _db.RosterAssignments
            .Where(a => a.OccurrenceId == occurrence.OccurrenceId
                && a.AssignmentStatusId != null)
            .Join(_db.AssignmentStatuses,
                a => a.AssignmentStatusId,
                s => s.AssignmentStatusId,
                (a, s) => new { s.IsTerminal })
            .Where(x => !x.IsTerminal)
            .CountAsync(cancellationToken);

        if (effectiveRequired == 0)
        {
            // Zero-duty occurrence: active (0) meets required (0).
            // §12.1.2. Classified as Filled by the mechanical rule.
            return FillClassification.Filled;
        }

        if (activeCount == 0)
        {
            return FillClassification.Unfilled;
        }

        if (activeCount >= effectiveRequired)
        {
            return FillClassification.Filled;
        }

        return FillClassification.PartiallyFilled;
    }

    private async Task<int> ResolveEffectiveRequiredCountAsync(
        ServiceOccurrence occurrence,
        CancellationToken cancellationToken)
    {
        // Schedule-sourced occurrence: the schedule determines the
        // service definition, whose ServiceDefinitionDuty rows supply
        // the inherited duty list.
        if (occurrence.ScheduleId is int scheduleId)
        {
            var schedule = await _db.ServiceSchedules
                .FirstOrDefaultAsync(s => s.ScheduleId == scheduleId, cancellationToken);

            if (schedule is null)
            {
                return 0;
            }

            var inherited = await _db.ServiceDefinitionDuties
                .Where(d => d.ServiceDefId == schedule.ServiceDefId && d.IsActive)
                .ToListAsync(cancellationToken);

            var overrides = await _db.ServiceOccurrenceDuties
                .Where(d => d.OccurrenceId == occurrence.OccurrenceId)
                .ToListAsync(cancellationToken);

            var byDuty = inherited.ToDictionary(d => d.DutyId, d => d.RequiredSlotCount);

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
                        ?? (byDuty.TryGetValue(ov.DutyId, out var existing) ? existing : 1);
                    byDuty[ov.DutyId] = slotCount;
                    continue;
                }

                // Unknown action — skip.
            }

            return byDuty.Values.Sum();
        }

        // Event-sourced occurrence: no schedule. §12 does not define a
        // duty list for these; the occurrence's required count is zero
        // unless ServiceOccurrenceDuty rows supply duties directly.
        var eventSourcedOverrides = await _db.ServiceOccurrenceDuties
            .Where(d => d.OccurrenceId == occurrence.OccurrenceId && d.Action == "Added")
            .ToListAsync(cancellationToken);

        return eventSourcedOverrides
            .Where(d => d.RequiredSlotCount is not null)
            .Sum(d => d.RequiredSlotCount!.Value);
    }

    // -----------------------------------------------------------------
    // Settings
    // -----------------------------------------------------------------

    private async Task<int?> ReadRequiredOutcomeStateIdAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting?.Value is null || !int.TryParse(setting.Value, out var id))
        {
            return null;
        }

        // Validate the OutcomeStateId exists.
        var exists = await _db.OutcomeStates
            .AnyAsync(o => o.OutcomeStateId == id, cancellationToken);

        return exists ? id : null;
    }

    private async Task<int?> ReadOptionalOutcomeStateIdAsync(
        string key,
        CancellationToken cancellationToken)
    {
        var setting = await _db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == key, cancellationToken);

        if (setting?.Value is null || !int.TryParse(setting.Value, out var id))
        {
            return null;
        }

        var exists = await _db.OutcomeStates
            .AnyAsync(o => o.OutcomeStateId == id, cancellationToken);

        return exists ? id : null;
    }
}
