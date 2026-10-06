namespace MknMs.Application.Common;

/// <summary>
/// Small helpers for the validation rules configuration services share.
/// </summary>
public static class ConfigurationValidation
{
    /// <summary>
    /// A name is required. Whitespace-only input is treated as missing.
    /// </summary>
    public static bool IsValidName(string? value) =>
        !string.IsNullOrWhiteSpace(value);

    /// <summary>
    /// A required non-negative integer. Zero is allowed; negatives are
    /// not. Used by slot counts and tier orders where zero is meaningful.
    /// </summary>
    public static bool IsNonNegative(int value) => value >= 0;

    /// <summary>
    /// A required positive integer. Used where zero would be meaningless
    /// (a schedule cannot require a slot count of zero).
    /// </summary>
    public static bool IsPositive(int value) => value > 0;
}
