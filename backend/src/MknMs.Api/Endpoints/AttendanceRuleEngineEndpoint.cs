
using MknMs.Application.Processes.Operations.AttendanceRuleEngine;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 13.0 — Attendance Rule Engine.
/// </summary>
/// <remarks>
/// One route, the manual invocation. A scheduled run has no HTTP
/// surface; the scheduler invokes it directly (§16.4). The manual
/// route names one rule and one acting admin, per the process
/// contract.
///
/// Specification: §13.6, §16.2, §16.4;
/// docs/processes/operations/13.0-attendance-rule-engine.md.
/// </remarks>
public static class AttendanceRuleEngineEndpoint
{
    public static IEndpointRouteBuilder MapAttendanceRuleEngineEndpoint(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
            "/api/processes/13.0/manual",
            async (
                AttendanceRuleEngineRequest? request,
                IAttendanceRuleEngineService service,
                CancellationToken cancellationToken) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Request body is required." });
                }

                if (request.RuleId <= 0 || request.ActingAdminId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "RuleId and ActingAdminId are required and must be positive.",
                    });
                }

                var command = new AttendanceRuleEngineCommand
                {
                    TriggerType = "Manual",
                    RuleId = request.RuleId,
                    ActingAdminId = request.ActingAdminId,
                };

                var result = await service.RunAsync(command, cancellationToken);

                return result.Status == "Success"
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            })
            .WithName("AttendanceRuleEngineManual")
            .WithTags("Processes")
            .WithOpenApi();

        return routes;
    }
}

/// <summary>
/// HTTP request body for the manual invocation.
/// </summary>
public sealed record AttendanceRuleEngineRequest
{
    public int RuleId { get; init; }
    public int ActingAdminId { get; init; }
}
