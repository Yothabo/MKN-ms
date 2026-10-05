namespace MknMs.Domain.D12_MaterializerRun;

/// <summary>
/// A single operational record of one Materializer run.
/// </summary>
/// <remarks>
/// Consistency constraints are enforced in the EF Core configuration:
/// Manual requires TriggeredBy; Scheduled requires TriggeredBy NULL.
/// Success requires CompletedAt; Failure requires ErrorDetail.
///
/// Specification: §4 (Materializer Run), §8.1.5.
/// </remarks>
public class MaterializerRun
{
    public int RunId { get; set; }

    public string TriggerType { get; set; } = null!;

    public int? TriggeredBy { get; set; }

    public DateTimeOffset StartedAt { get; set; }

    public DateTimeOffset? CompletedAt { get; set; }

    public int SchedulesEvaluated { get; set; }

    public int OccurrencesCreated { get; set; }

    public string Status { get; set; } = null!;

    public string? ErrorDetail { get; set; }

    // Navigation
    public Admin? TriggeredByAdmin { get; set; }
}
