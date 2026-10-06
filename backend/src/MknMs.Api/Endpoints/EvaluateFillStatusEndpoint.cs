using MknMs.Application.Processes.Operations.EvaluateFillStatus;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 10.0 — Evaluate Fill Status.
/// </summary>
/// <remarks>
/// One route, one process. Accepts either a specific occurrence or a
/// date range. Dates are ISO 8601 (yyyy-MM-dd).
///
/// Specification: §12, docs/processes/operations/10.0-evaluate-fill-status.md.
/// </remarks>
public static class EvaluateFillStatusEndpoint
{
    public static IEndpointRouteBuilder MapEvaluateFillStatusEndpoint(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
            "/api/processes/10.0/evaluate",
            async (
                EvaluateFillStatusRequest? request,
                IEvaluateFillStatusService service,
                CancellationToken cancellationToken) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Request body is required." });
                }

                DateOnly? from = null;
                DateOnly? to = null;

                if (!string.IsNullOrWhiteSpace(request.FromDate))
                {
                    if (!DateOnly.TryParse(request.FromDate, out var parsedFrom))
                    {
                        return Results.BadRequest(new
                        {
                            error = "FromDate must be an ISO 8601 date (yyyy-MM-dd).",
                        });
                    }
                    from = parsedFrom;
                }

                if (!string.IsNullOrWhiteSpace(request.ToDate))
                {
                    if (!DateOnly.TryParse(request.ToDate, out var parsedTo))
                    {
                        return Results.BadRequest(new
                        {
                            error = "ToDate must be an ISO 8601 date (yyyy-MM-dd).",
                        });
                    }
                    to = parsedTo;
                }

                if (from is DateOnly f && to is DateOnly t && f > t)
                {
                    return Results.BadRequest(new
                    {
                        error = "FromDate must not be after ToDate.",
                    });
                }

                var command = new EvaluateFillStatusCommand
                {
                    OccurrenceId = request.OccurrenceId,
                    FromDate = from,
                    ToDate = to,
                };

                var result = await service.RunAsync(command, cancellationToken);

                return result.Status == "Success"
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            })
            .WithName("EvaluateFillStatus")
            .WithTags("Processes")
            .WithOpenApi();

        return routes;
    }
}

/// <summary>
/// HTTP request body for the evaluate-fill-status endpoint.
/// </summary>
public sealed record EvaluateFillStatusRequest
{
    public int? OccurrenceId { get; init; }
    public string? FromDate { get; init; }
    public string? ToDate { get; init; }
}
