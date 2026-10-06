namespace MknMs.Application.Common;

/// <summary>
/// Result of a configuration operation. Either succeeds and carries a
/// value, or fails and carries a validation error.
/// </summary>
/// <remarks>
/// Configuration operations return this type rather than throwing for
/// expected failures such as validation. Unexpected failures (a
/// database error, a missing referenced entity) still throw.
/// </remarks>
public sealed class ConfigurationResult<T>
{
    private ConfigurationResult(T? value, ConfigurationError? error)
    {
        Value = value;
        Error = error;
    }

    public T? Value { get; }

    public ConfigurationError? Error { get; }

    public bool IsSuccess => Error is null;

    public static ConfigurationResult<T> Success(T value) =>
        new(value, null);

    public static ConfigurationResult<T> Failure(ConfigurationError error) =>
        new(default, error);
}

/// <summary>
/// A validation error returned by a configuration operation.
/// </summary>
/// <remarks>
/// The Code is a stable, machine-readable identifier the API maps to a
/// specific HTTP status. The Message is administrator-facing text.
/// </remarks>
public sealed record ConfigurationError(string Code, string Message);
