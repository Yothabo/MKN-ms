
namespace MknMs.Domain.D14_ConfigurationAudit;

/// <summary>
/// The permanent record of a configuration deactivation or soft
/// delete.
/// </summary>
/// <remarks>
/// No lifecycle flags. Audit records are permanent facts. NotifiedAt
/// is set once the authority-notification sweep has processed the
/// entry (9.0 Path 2).
///
/// Specification: §4 (Configuration Audit Log), §9.7, §14.1.3,
/// §15.8.14.
/// </remarks>
public class ConfigurationAuditLog
{
    public int AuditId { get; set; }

    public string EntityType { get; set; } = null!;

    public int EntityId { get; set; }

    public string EntityName { get; set; } = null!;

    public string Action { get; set; } = null!;

    public string? Reason { get; set; }

    public int InitiatedByAdminId { get; set; }

    public int ApprovedByAdminId { get; set; }

    public DateTimeOffset InitiatedAt { get; set; }

    public DateTimeOffset ApprovedAt { get; set; }

    public string ConsequencesPreviewed { get; set; } = null!;

    public string ConsequencesOccurred { get; set; } = null!;

    public DateTimeOffset? NotifiedAt { get; set; }

    // Navigation
    public Admin InitiatedByAdmin { get; set; } = null!;

    public Admin ApprovedByAdmin { get; set; } = null!;
}
