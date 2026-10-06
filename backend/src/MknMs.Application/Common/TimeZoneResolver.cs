using Npgsql;

namespace MknMs.Application.Common;

/// <summary>
/// Resolves the configured application timezone used to derive
/// operational dates from an instant.
/// </summary>
/// <remarks>
/// The specification §7 requires that occurrence dates are generated
/// using "the configured system/application timezone," not UTC. The
/// same rule governs the date derived by the assignment engine for
/// age and tenure criteria evaluation, and the timezone the Quartz
/// scheduler uses to evaluate its cron triggers.
///
/// The timezone is read from SystemSetting under the key
/// ApplicationTimeZone, holding an IANA timezone ID (for example,
/// Africa/Johannesburg). The setting is not listed among the required
/// settings in the specification's amendment set, so its absence does
/// not cause a process to refuse to run. When it is absent, or when it
/// holds a value that is not a recognized timezone ID, the resolver
/// returns UTC.
///
/// Two entry points:
///   - ResolveAsync(MknDbContext, ...) — used by processes that have a
///     DbContext available at run time.
///   - ResolveFromConnectionAsync(NpgsqlConnection, ...) — used by
///     composition code that runs before the service provider is
///     built, and therefore cannot resolve a scoped DbContext.
///
/// Both read the same setting and apply the same fallback logic, so
/// the process's view of "today" and the scheduler's trigger timezone
/// are always derived from the same configuration value.
///
/// Specification: §7, §11.
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

        return InterpretValue(setting?.Value);
    }

    public static async Task<TimeZoneInfo> ResolveFromConnectionAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken = default)
    {
        var wasClosed = connection.State != System.Data.ConnectionState.Open;
        if (wasClosed)
        {
            await connection.OpenAsync(cancellationToken);
        }

        try
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT value FROM system_setting WHERE key = @key";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@key";
            parameter.Value = ApplicationTimeZoneKey;
            command.Parameters.Add(parameter);

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var value = result as string;

            return InterpretValue(value);
        }
        finally
        {
            if (wasClosed && connection.State == System.Data.ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
        }
    }

    private static TimeZoneInfo InterpretValue(string? rawValue)
    {
        if (string.IsNullOrWhiteSpace(rawValue))
        {
            return Fallback;
        }

        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(rawValue.Trim());
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
