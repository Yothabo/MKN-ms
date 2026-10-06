namespace MknMs.Application.Processes.Operations.EvaluateFillStatus;

/// <summary>
/// The Evaluate Fill Status process (10.0).
/// </summary>
/// <remarks>
/// Computes each occurrence's fill state and writes it to
/// ServiceOccurrence.FillStatusId. The only field written. No other
/// store is touched. No process is invoked.
///
/// Refuses to run entirely if any of the three required mapping
/// settings is absent — no partial evaluation.
///
/// Specification: §12.
/// </remarks>
public interface IEvaluateFillStatusService
{
    Task<EvaluateFillStatusResult> RunAsync(
        EvaluateFillStatusCommand command,
        CancellationToken cancellationToken = default);
}
