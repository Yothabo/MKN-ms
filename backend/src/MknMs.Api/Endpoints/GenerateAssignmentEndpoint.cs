using MknMs.Application.Processes.Operations.GenerateAssignment;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 5.0 — Generate Assignment.
/// </summary>
/// <remarks>
/// One endpoint. Accepts a specific occurrence or a date range and
/// invokes the service. Performs no business logic of its own — but it
/// does validate that the request body is well-formed before invoking
/// the service, so that a malformed date is reported as a malformed
/// date rather than surfacing as the service's "neither occurrence nor
/// range supplied" failure.
///
/// Specification: §10, docs/processes/operations/5.0-generate-assignment.md.
/// </remarks>
public static class GenerateAssignmentEndpoint
{
    public static IEndpointRouteBuilder MapGenerateAssignmentEndpoint(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
            "/api/processes/5.0/generate",
            async (
                GenerateAssignmentRequest? request,
                IGenerateAssignmentService service,
                CancellationToken cancellationToken) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new
                    {
                        error = "Request body is required.",
                    });
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

                var command = new GenerateAssignmentCommand
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
            .WithName("GenerateAssignment")
            .WithTags("Processes")
            .WithOpenApi();

        return routes;
    }
}

/// <summary>
/// HTTP request body for the generate-assignment endpoint.
/// </summary>
/// <remarks>
/// Supply exactly one of:
///   - occurrenceId, to fill one specific occurrence.
///   - fromDate and toDate, to fill every occurrence in the range.
///
/// Dates are ISO 8601 (yyyy-MM-dd).
/// </remarks>
public sealed record GenerateAssignmentRequest
{
    public int? OccurrenceId { get; init; }

    public string? FromDate { get; init; }

    public string? ToDate { get; init; }
}
