using MknMs.Application.Processes.Operations.RecordAttendance;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for process 6.0 — Record Attendance.
/// </summary>
/// <remarks>
/// One route. Accepts a (memberId, occurrenceId) pair and records the
/// member's presence at the occurrence. No field distinguishes a tap
/// from an admin manual entry; the row is the same either way.
///
/// Specification: §13, docs/processes/operations/6.0-record-attendance.md.
/// </remarks>
public static class RecordAttendanceEndpoint
{
    public static IEndpointRouteBuilder MapRecordAttendanceEndpoint(
        this IEndpointRouteBuilder routes)
    {
        routes.MapPost(
            "/api/processes/6.0/record",
            async (
                RecordAttendanceRequest? request,
                IRecordAttendanceService service,
                CancellationToken cancellationToken) =>
            {
                if (request is null)
                {
                    return Results.BadRequest(new { error = "Request body is required." });
                }

                if (request.MemberId <= 0 || request.OccurrenceId <= 0)
                {
                    return Results.BadRequest(new
                    {
                        error = "MemberId and OccurrenceId are required and must be positive.",
                    });
                }

                var command = new RecordAttendanceCommand
                {
                    MemberId = request.MemberId,
                    OccurrenceId = request.OccurrenceId,
                };

                var result = await service.RunAsync(command, cancellationToken);

                return result.Status == "Success"
                    ? Results.Ok(result)
                    : Results.UnprocessableEntity(result);
            })
            .WithName("RecordAttendance")
            .WithTags("Processes")
            .WithOpenApi();

        return routes;
    }
}

/// <summary>
/// HTTP request body for the record-attendance endpoint.
/// </summary>
public sealed record RecordAttendanceRequest
{
    public int MemberId { get; init; }
    public int OccurrenceId { get; init; }
}
