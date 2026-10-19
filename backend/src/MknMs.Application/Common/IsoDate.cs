namespace MknMs.Application.Common;

/// <summary>
/// Pure ISO date and timestamp parsers used by the HTTP adapter.
/// </summary>
/// <remarks>
/// The HTTP adapter's <c>TryParseIsoDate</c> and <c>TryParseTimestamp</c>
/// helpers build the documented <c>BadRequest</c> response on failure;
/// the parsing itself is here so it can be unit-tested without an
/// HTTP pipeline.
///
/// Specification: docs/api/adapter-contract.md (request field validation).
/// </remarks>
public static class IsoDate
{
    public static bool TryParse(string? value, out DateOnly result) =>
        DateOnly.TryParse(value, out result);

    public static bool TryParseTimestamp(string? value, out DateTimeOffset result) =>
        DateTimeOffset.TryParse(value, out result);
}
