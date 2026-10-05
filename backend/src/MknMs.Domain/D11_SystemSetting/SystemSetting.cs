namespace MknMs.Domain.D11_SystemSetting;

/// <summary>
/// A scalar, non-list configuration value. The existence of each
/// recognized key is fixed; the value is entirely administrator-set
/// with no built-in default.
/// </summary>
/// <remarks>
/// A required setting with no value causes the consuming process to
/// refuse to run and surface a configuration error (the required-
/// setting rule from §2).
///
/// Currently expected keys: OccurrenceHorizonDays, InitialAssignmentStatusID,
/// ConfirmationTimeoutHours, the four OutcomeState*ID settings,
/// NotificationChannel, and supporting thresholds.
///
/// Specification: §4 (System Setting), §15.3.
/// </remarks>
public class SystemSetting
{
    public string Key { get; set; } = null!;

    public string? Value { get; set; }

    public bool Required { get; set; }

    public string? Description { get; set; }
}
