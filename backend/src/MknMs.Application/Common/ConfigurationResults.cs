namespace MknMs.Application.Common;

/// <summary>
/// Well-known error codes emitted by configuration services.
/// </summary>
/// <remarks>
/// These codes are the vocabulary the API uses to decide response
/// status and are stable identifiers. New codes may be added; existing
/// codes do not change meaning.
/// </remarks>
public static class ConfigurationErrorCodes
{
    public const string NotFound = "not_found";

    public const string DuplicateName = "duplicate_name";

    public const string InvalidValue = "invalid_value";

    public const string ReferencedEntityInactive = "referenced_entity_inactive";

    public const string ReferencedEntityMissing = "referenced_entity_missing";

    public const string AlreadyInactive = "already_inactive";

    public const string AlreadyActive = "already_active";
}
