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
/// Africa/Johannesburg). §15.3.11: the setting is required. There is
/// no silent fallback. When it is absent, the process surfaces
/// ConfigurationMissing. When it holds an unrecognised value, the
/// process surfaces ConfigurationInvalid.
///
/// Two entry points:
///   - ResolveAsync(MknDbContext, ...) — used by processes that have a
///     DbContext available at run time. Returns a result carrying
///     either the resolved zone or a configuration error code.
///   - ResolveFromConnectionAsync(NpgsqlConnection, ...) — used by
///     composition code that runs before the service provider is
///     built. §15.3.11 states the scheduler refuses to start when the
///     setting is absent or invalid; this method throws
///     InvalidOperationException to enforce that. Program.cs lets the
///     throw propagate.
///
/// Specification: §7, §11, §15.3.11, §15.8.33.
/// </remarks>
public static class TimeZoneResolver
{
    public const string ApplicationTimeZoneKey = "ApplicationTimeZone";

    public static async Task<TimeZoneResolutionResult> ResolveAsync(
        MknDbContext db,
        CancellationToken cancellationToken = default)
    {
        var setting = await db.SystemSettings
            .FirstOrDefaultAsync(s => s.Key == ApplicationTimeZoneKey, cancellationToken);

        if (setting?.Value is null || string.IsNullOrWhiteSpace(setting.Value))
        {
            return TimeZoneResolutionResult.Missing();
        }

        var trimmed = setting.Value.Trim();

        try
        {
            var zone = TimeZoneInfo.FindSystemTimeZoneById(trimmed);
            return TimeZoneResolutionResult.Success(zone);
        }
        catch (TimeZoneNotFoundException)
        {
            return TimeZoneResolutionResult.Invalid(trimmed);
        }
        catch (InvalidTimeZoneException)
        {
            return TimeZoneResolutionResult.Invalid(trimmed);
        }
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

            if (value is null || string.IsNullOrWhiteSpace(value))
            {
                throw new InvalidOperationException(
                    $"Required setting '{ApplicationTimeZoneKey}' is not configured. "
                    + "The application cannot start.");
            }

            var trimmed = value.Trim();

            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(trimmed);
            }
            catch (TimeZoneNotFoundException)
            {
                throw new InvalidOperationException(
                    $"Setting '{ApplicationTimeZoneKey}' has unrecognised value "
                    + $"'{trimmed}'. The application cannot start.");
            }
            catch (InvalidTimeZoneException)
            {
                throw new InvalidOperationException(
                    $"Setting '{ApplicationTimeZoneKey}' has invalid value "
                    + $"'{trimmed}'. The application cannot start.");
            }
        }
        finally
        {
            if (wasClosed && connection.State == System.Data.ConnectionState.Open)
            {
                await connection.CloseAsync();
            }
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

/// <summary>
/// Outcome of a runtime timezone resolution.
/// </summary>
/// <remarks>
/// Either Success and a TimeZoneInfo, or Failure and a configuration
/// error code. The caller puts the code into its own Failure result
/// (§15.3.11, handover §4.4).
/// </remarks>
public sealed class TimeZoneResolutionResult
{
    private TimeZoneResolutionResult(TimeZoneInfo? zone, string? errorCode, string? errorDetail)
    {
        Zone = zone;
        ErrorCode = errorCode;
        ErrorDetail = errorDetail;
    }

    public TimeZoneInfo? Zone { get; }

    public string? ErrorCode { get; }

    public string? ErrorDetail { get; }

    public bool IsSuccess => ErrorCode is null;

    public static TimeZoneResolutionResult Success(TimeZoneInfo zone) =>
        new(zone, null, null);

    public static TimeZoneResolutionResult Missing() =>
        new(null,
            ConfigurationErrorCodes.ConfigurationMissing,
            $"Required setting '{TimeZoneResolver.ApplicationTimeZoneKey}' is not configured.");

    public static TimeZoneResolutionResult Invalid(string value) =>
        new(null,
            ConfigurationErrorCodes.ConfigurationInvalid,
            $"Setting '{TimeZoneResolver.ApplicationTimeZoneKey}' has unrecognised value '{value}'.");
}
