using MknMs.Application.Processes.Operations.ManageConfirmation;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 7.0 — Manage Confirmation.
/// </summary>
/// <remarks>
/// Two routes, one process:
///
///   - POST /api/processes/7.0/respond — a member has confirmed or
///     declined. The caller supplies the assignment and the status ID
///     the response maps to. The endpoint performs no mapping of
///     "confirm"/"decline" to status IDs; that mapping is administrator
///     configuration and belongs to the caller.
///
///   - POST /api/processes/7.0/sweep — the scheduled timeout check. The
///     caller supplies the status ID a timed-out assignment maps to.
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

                if (request.AssignmentId is null || request.AssignmentId <= 0
                    || request.TargetStatusId is null || request.TargetStatusId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "AssignmentId and TargetStatusId are required and must be positive.",
                    });
                }

                var command = new ManageConfirmationCommand
                {
                    AssignmentId = request.AssignmentId,
                    TargetStatusId = request.TargetStatusId,
                    IsSweep = false,
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
                ManageConfirmationSweepRequest? request,
                IManageConfirmationService service,
                CancellationToken cancellationToken) =>
            {
                if (request is null || request.TargetStatusId is null
                    || request.TargetStatusId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "TargetStatusId is required and must be positive.",
                    });
                }

                var command = new ManageConfirmationCommand
                {
                    AssignmentId = null,
                    TargetStatusId = request.TargetStatusId,
                    IsSweep = true,
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
/// HTTP request body for the respond endpoint.
/// </summary>
public sealed record ManageConfirmationRespondRequest
{
    public int? AssignmentId { get; init; }
    public int? TargetStatusId { get; init; }
}

/// <summary>
/// HTTP request body for the sweep endpoint.
/// </summary>
public sealed record ManageConfirmationSweepRequest
{
    public int? TargetStatusId { get; init; }
}
