namespace MknMs.Application.Common;

/// <summary>
/// Resolves the configured application timezone used to derive
/// operational dates from an instant.
/// </summary>
/// <remarks>
/// The specification §7 requires that occurrence dates are generated
/// using "the configured system/application timezone," not UTC. The
/// same rule governs the date derived by the assignment engine for
/// age and tenure criteria evaluation.
///
/// The timezone is read from SystemSetting under the key
/// ApplicationTimeZone, holding an IANA timezone ID (for example,
/// Africa/Johannesburg). The setting is not listed among the required
/// settings in the specification's amendment set, so its absence does
/// not cause a process to refuse to run. When it is absent, or when it
/// holds a value that is not a recognized timezone ID, the resolver
/// returns UTC — matching the behaviour that was implicit before this
/// resolver existed, and giving a configured value precedence when
/// one is present.
///
/// The setting is read on each resolution, so changing it takes
/// effect on the next process run. This matches §7's rule that a
/// change to a setting takes effect on the next run and is not itself
/// a trigger.
/// </remarks>
public static class TimeZoneResolver
{
    public const string ApplicationTimeZoneKey = "ApplicationTimeZone";

    private static readonly TimeZoneInfo Fallback = TimeZoneInfo.Utc;

    public static async Task<TimeZoneInfo> ResolveAsync(
        MknDbContext db,
        CancellationToken cancellationToken = default)
    {
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == ApplicationTimeZoneKey, cancellationToken);

        if (setting?.Value is null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return Fallback;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(setting.Value.Trim());
        }
        catch (TimeZoneNotFoundException)
        {
            return Fallback;
        }
        catch (InvalidTimeZoneException)
        {
            return Fallback;
        }
    }

    /// <summary>
    /// Converts an instant to the calendar date it falls on in the
    /// supplied timezone.
    /// </summary>
    public static DateOnly ToDateInTimeZone(DateTimeOffset instant, TimeZoneInfo timeZone)
    {
        var local = TimeZoneInfo.ConvertTime(instant, timeZone);
        return DateOnly.FromDateTime(local.DateTime);
    }
}
