namespace MknMs.Application.Processes.Operations.MaterializeOccurrences;

/// <summary>
/// Inputs to one Materialize Occurrences run.
/// </summary>
/// <remarks>
/// TriggerType is Scheduled when invoked by the application's scheduler;
/// Manual when invoked by an administrator. TriggeredBy is required for
/// Manual triggers and must be null for Scheduled triggers — this rule
/// is enforced by a CHECK constraint on materializer_run.
///
/// HorizonDaysOverride, when set, replaces the value from
/// system_setting for this run only. Used by the manual trigger to
/// reach further ahead than the standing horizon. It does not modify
/// the stored setting.
/// </remarks>
public sealed record MaterializeOccurrencesCommand
{
    public required string TriggerType { get; init; }

    public int? TriggeredByAdminId { get; init; }

    public int? HorizonDaysOverride { get; init; }
}
