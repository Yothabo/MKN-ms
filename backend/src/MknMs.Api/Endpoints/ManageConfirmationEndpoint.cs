using MknMs.Application.Processes.Operations.ManageConfirmation;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 7.0 — Manage Confirmation.
/// </summary>
/// <remarks>
/// Two routes, one process:
///
///   - POST /api/processes/7.0/respond — a member has confirmed or
///     declined. The caller names the operation ("Confirm" or
///     "Decline") and the assignment. The target status is read from
///     SystemSetting by the service, not supplied by the caller.
///
///   - POST /api/processes/7.0/sweep — the scheduled timeout check.
///     The service finds every timed-out assignment and transitions it
///     to the status named by SystemSetting.TimedOutStatusID.
///
/// Specification: §11, docs/processes/operations/7.0-manage-confirmation.md.
/// </remarks>
public static class ManageConfirmationEndpoint
{
    public static IEndpointRouteBuilder MapManageConfirmationEndpoint(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
            "/api/processes/7.0/respond",
            async (
                ManageConfirmationRespondRequest? request,
                IManageConfirmationService service,
                CancellationToken cancellationToken) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Request body is required." });
                }

                if (request.AssignmentId is null || request.AssignmentId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "AssignmentId is required and must be positive.",
                    });
                }

                if (string.IsNullOrWhiteSpace(request.Operation))
                {
                    return Results.BadRequest(new
                    {
                        error = "Operation is required. Must be \"Confirm\" or \"Decline\".",
                    });
                }

                ManageConfirmationOperation operation;
                if (string.Equals(request.Operation, "Confirm", StringComparison.OrdinalIgnoreCase))
                {
                    operation = ManageConfirmationOperation.Confirm;
                }
                else if (string.Equals(request.Operation, "Decline", StringComparison.OrdinalIgnoreCase))
                {
                    operation = ManageConfirmationOperation.Decline;
                }
                else
                {
                    return Results.BadRequest(new
                    {
                        error = "Operation must be \"Confirm\" or \"Decline\".",
                    });
                }

                var command = new ManageConfirmationCommand
                {
                    Operation = operation,
                    AssignmentId = request.AssignmentId,
                };

                var result = await service.RunAsync(command, cancellationToken);

                return result.Status == "Success"
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            })
            .WithName("ManageConfirmationRespond")
            .WithTags("Processes")
            .WithOpenApi();

        routes.MapPost(
            "/api/processes/7.0/sweep",
            async (
                IManageConfirmationService service,
                CancellationToken cancellationToken) =>
            {
                var command = new ManageConfirmationCommand
                {
                    Operation = ManageConfirmationOperation.Sweep,
                    AssignmentId = null,
                };

                var result = await service.RunAsync(command, cancellationToken);

                return result.Status == "Success"
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            })
            .WithName("ManageConfirmationSweep")
            .WithTags("Processes")
            .WithOpenApi();

        return routes;
    }
}

/// <summary>
/// HTTP request body for the respond route.
/// </summary>
/// <remarks>
/// Operation is "Confirm" or "Decline". The target status the operation
/// resolves to is administrator configuration, read by the service from
/// SystemSetting; it is not part of this request.
/// </remarks>
public sealed record ManageConfirmationRespondRequest
{
    public int? AssignmentId { get; init; }
    public string? Operation { get; init; }
}
