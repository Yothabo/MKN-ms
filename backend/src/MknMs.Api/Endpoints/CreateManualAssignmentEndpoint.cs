using MknMs.Application.Processes.Operations.CreateManualAssignment;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 12.0 — Create Manual Assignment.
/// </summary>
/// <remarks>
/// One route. The administrator selects a member, a duty, an
/// occurrence, and optionally a target AssignmentStatus. When the
/// target status is null, the row enters 7.0's lifecycle and 9.0 is
/// invoked. When the target status is set, no notification is
/// dispatched.
///
/// Specification: §17, docs/processes/operations/12.0-manual-assignment.md.
/// </remarks>
public static class CreateManualAssignmentEndpoint
{
    public static IEndpointRouteBuilder MapCreateManualAssignmentEndpoint(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
            "/api/processes/12.0/manual",
            async (
                CreateManualAssignmentRequest? request,
                ICreateManualAssignmentService service,
                CancellationToken cancellationToken) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Request body is required." });
                }

                if (request.MemberId <= 0
                    || request.DutyId <= 0
                    || request.OccurrenceId <= 0
                    || request.ActingAdminId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "MemberId, DutyId, OccurrenceId, and ActingAdminId are " +
                                "required and must be positive.",
                    });
                }

                var command = new CreateManualAssignmentCommand
                {
                    MemberId = request.MemberId,
                    DutyId = request.DutyId,
                    OccurrenceId = request.OccurrenceId,
                    TargetStatusId = request.TargetStatusId,
                    ActingAdminId = request.ActingAdminId,
                };

                var result = await service.RunAsync(command, cancellationToken);

                return result.Status == "Success"
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            })
            .WithName("CreateManualAssignment")
            .WithTags("Processes")
            .WithOpenApi();

        return routes;
    }
}

/// <summary>
/// HTTP request body for the manual-assignment endpoint.
/// </summary>
public sealed record CreateManualAssignmentRequest
{
    public int MemberId { get; init; }
    public int DutyId { get; init; }
    public int OccurrenceId { get; init; }
    public int? TargetStatusId { get; init; }
    public int ActingAdminId { get; init; }
}
