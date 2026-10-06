using MknMs.Application.Processes.Operations.MaterializeOccurrences;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 11.0 — Materialize Occurrences.
/// </summary>
/// <remarks>
/// One endpoint, one process. The endpoint accepts the manual trigger's
/// inputs and invokes the service. It does not perform any business
/// logic of its own.
///
/// See docs/api/endpoints.md and docs/processes/operations/11.0-materialize-occurrences.md.
/// </remarks>
public static class MaterializeOccurrencesEndpoint
{
    public static IEndpointRouteBuilder MapMaterializeOccurrencesEndpoint(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
            "/api/processes/11.0/materialize",
            async (
                MaterializeOccurrencesRequest? request,
                IMaterializeOccurrencesService service,
                CancellationToken cancellationToken) =>
            {
                var requestValue = request ?? new MaterializeOccurrencesRequest();

                var triggerType = requestValue.TriggerType ?? "Manual";

                if (triggerType != "Manual" && triggerType != "Scheduled")
                {
                    return Results.BadRequest(new
                    {
                        error = "TriggerType must be \"Manual\" or \"Scheduled\".",
                    });
                }

                var command = new MaterializeOccurrencesCommand
                {
                    TriggerType = triggerType,
                    TriggeredByAdminId = requestValue.TriggeredByAdminId,
                    HorizonDaysOverride = requestValue.HorizonDaysOverride,
                };

                var result = await service.RunAsync(command, cancellationToken);

                return result.Status == "Success"
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            })
            .WithName("MaterializeOccurrences")
            .WithTags("Processes")
            .WithOpenApi();

        return routes;
    }
}

/// <summary>
/// HTTP request body for the materialize-occurrences endpoint.
/// </summary>
/// <remarks>
/// All fields are optional. When TriggerType is omitted, "Manual" is
/// assumed. When HorizonDaysOverride is omitted, the value from
/// system_setting is used. TriggeredByAdminId is required only when
/// TriggerType is "Manual" — the service enforces that rule.
/// </remarks>
public sealed record MaterializeOccurrencesRequest
{
    public string? TriggerType { get; init; }

    public int? TriggeredByAdminId { get; init; }

    public int? HorizonDaysOverride { get; init; }
}
