using Microsoft.AspNetCore.Mvc;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureBranch;
using MknMs.Application.Processes.Configuration.ConfigureDuty;
using MknMs.Application.Processes.Configuration.ConfigureDutyRules;
using MknMs.Application.Processes.Configuration.ConfigureRole;
using MknMs.Application.Processes.Configuration.ConfigureServiceDefinition;
using MknMs.Application.Processes.Configuration.ConfigureServiceDutyAndSchedule;
using MknMs.Application.Processes.Configuration.ConfigureTimeSlot;
using MknMs.Application.Processes.Configuration.Lookup_ServiceType;
using MknMs.Application.Processes.Configuration.Lookup_TimeOfDay;
using MknMs.Application.Processes.Configuration.ManageEligibility;
using MknMs.Application.Processes.Configuration.ManageEvent;
using MknMs.Application.Processes.Configuration.ManageEventDuty;
using MknMs.Application.Processes.Configuration.ManageIdentifierHistory;
using MknMs.Application.Processes.Configuration.ManageMemberRecord;
using MknMs.Application.Processes.Configuration.ManageProgram;
using MknMs.Application.Processes.Configuration.ManageProgramItem;

namespace MknMs.Api.Endpoints;

/// <summary>
/// HTTP surface for the configuration layer — processes 1.0, 2.0, 3.0,
/// 4.0, and 8.0. One endpoint per operation; one process per route
/// prefix.
/// </summary>
/// <remarks>
/// The endpoint layer performs no business logic of its own. It maps
/// HTTP requests to service calls, translates ConfigurationResult to
/// HTTP status, and returns the response.
/// </remarks>
public static class ConfigurationEndpoints
{
    public static IEndpointRouteBuilder MapConfigurationEndpoints(this IEndpointRouteBuilder routes)
    {
        MapLookups(routes);
        MapConfigureVocabulary(routes);
        MapConfigureDutyRules(routes);
        MapManageMembership(routes);
        MapManageEligibility(routes);
        MapManageEventsAndPrograms(routes);
        return routes;
    }

    // -----------------------------------------------------------------
    // Helpers
    // -----------------------------------------------------------------

    private static IResult ToHttpResult<T>(ConfigurationResult<T> result)
    {
        if (result.IsSuccess)
        {
            return Results.Ok(result.Value);
        }

        return result.Error!.Code switch
        {
            ConfigurationErrorCodes.NotFound =>
                Results.NotFound(new { error = result.Error }),
            ConfigurationErrorCodes.DuplicateName =>
                Results.Conflict(new { error = result.Error }),
            ConfigurationErrorCodes.AlreadyInactive or ConfigurationErrorCodes.AlreadyActive =>
                Results.Conflict(new { error = result.Error }),
            ConfigurationErrorCodes.ReferencedEntityMissing or
            ConfigurationErrorCodes.ReferencedEntityInactive or
            ConfigurationErrorCodes.InvalidValue =>
                Results.UnprocessableEntity(new { error = result.Error }),
            _ =>
                Results.BadRequest(new { error = result.Error }),
        };
    }

    // -----------------------------------------------------------------
    // Lookups
    // -----------------------------------------------------------------

    private static void MapLookups(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/lookups/time-of-day", async (
            ITimeOfDayService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        routes.MapPost("/api/lookups/time-of-day", async (
            [FromBody] CreateLookupRequest request,
            ITimeOfDayService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(request.Name, cancellationToken)));

        routes.MapGet("/api/lookups/service-type", async (
            IServiceTypeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        routes.MapPost("/api/lookups/service-type", async (
            [FromBody] CreateLookupRequest request,
            IServiceTypeService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(request.Name, cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 1.0 Configure Vocabulary
    // -----------------------------------------------------------------

    private static void MapConfigureVocabulary(IEndpointRouteBuilder routes)
    {
        // 1.1 Role
        routes.MapGet("/api/config/1.0/roles", async (
            bool? includeInactive,
            IConfigureRoleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/1.0/roles", async (
            [FromBody] CreateNamedEntityRequest request,
            IConfigureRoleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(request.Name, cancellationToken)));

        routes.MapPut("/api/config/1.0/roles/{id:int}", async (
            int id,
            [FromBody] CreateNamedEntityRequest request,
            IConfigureRoleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RenameAsync(id, request.Name, cancellationToken)));

        routes.MapDelete("/api/config/1.0/roles/{id:int}", async (
            int id,
            IConfigureRoleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));

        // 1.2 Duty
        routes.MapGet("/api/config/1.0/duties", async (
            bool? includeInactive,
            IConfigureDutyService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/1.0/duties", async (
            [FromBody] CreateNamedEntityRequest request,
            IConfigureDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(request.Name, cancellationToken)));

        routes.MapPut("/api/config/1.0/duties/{id:int}", async (
            int id,
            [FromBody] CreateNamedEntityRequest request,
            IConfigureDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RenameAsync(id, request.Name, cancellationToken)));

        routes.MapDelete("/api/config/1.0/duties/{id:int}", async (
            int id,
            IConfigureDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));

        // 1.3 Branch
        routes.MapGet("/api/config/1.0/branches", async (
            bool? includeInactive,
            IConfigureBranchService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/1.0/branches", async (
            [FromBody] CreateBranchRequest request,
            IConfigureBranchService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(request.Name, request.Location, cancellationToken)));

        routes.MapPut("/api/config/1.0/branches/{id:int}", async (
            int id,
            [FromBody] CreateBranchRequest request,
            IConfigureBranchService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateAsync(id, request.Name, request.Location, cancellationToken)));

        routes.MapDelete("/api/config/1.0/branches/{id:int}", async (
            int id,
            IConfigureBranchService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));

        // 1.4 Time Slot
        routes.MapGet("/api/config/1.0/time-slots", async (
            int? branchId,
            bool? includeInactive,
            IConfigureTimeSlotService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(branchId, includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/1.0/time-slots", async (
            [FromBody] CreateTimeSlotRequest request,
            IConfigureTimeSlotService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                request.BranchId, request.DayOfWeek, request.TimeOfDayId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/time-slots/{id:int}", async (
            int id,
            IConfigureTimeSlotService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));

        // 1.5 Service Definition
        routes.MapGet("/api/config/1.0/service-definitions", async (
            bool? includeInactive,
            IConfigureServiceDefinitionService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/1.0/service-definitions", async (
            [FromBody] CreateServiceDefinitionRequest request,
            IConfigureServiceDefinitionService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                request.Name, request.ServiceTypeId, request.OwningBranchId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/service-definitions/{id:int}", async (
            int id,
            IConfigureServiceDefinitionService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));

        // 1.6 Service Definition Duty and Service Schedule
        routes.MapGet("/api/config/1.0/service-definitions/{id:int}/duties", async (
            int id,
            bool? includeInactive,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListDutiesAsync(id, includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/1.0/service-definitions/{id:int}/duties", async (
            int id,
            [FromBody] AddServiceDutyRequest request,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.AddDutyAsync(id, request.DutyId, request.RequiredSlotCount, cancellationToken)));

        routes.MapDelete("/api/config/1.0/service-definitions/{id:int}/duties/{dutyId:int}", async (
            int id,
            int dutyId,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RemoveDutyAsync(id, dutyId, cancellationToken)));

        routes.MapGet("/api/config/1.0/service-schedules", async (
            int? serviceDefId,
            bool? includeInactive,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListSchedulesAsync(serviceDefId, includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/1.0/service-schedules", async (
            [FromBody] CreateScheduleRequest request,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateScheduleAsync(
                request.ServiceDefId, request.TimeSlotId, TimeOnly.Parse(request.StartTime), cancellationToken)));

        routes.MapDelete("/api/config/1.0/service-schedules/{id:int}", async (
            int id,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateScheduleAsync(id, cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 2.0 Configure Duty Rules
    // -----------------------------------------------------------------

    private static void MapConfigureDutyRules(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/config/2.0/duties/{dutyId:int}/rules", async (
            int dutyId,
            bool? includeInactive,
            IConfigureDutyRulesService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForDutyAsync(dutyId, includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/2.0/duties/{dutyId:int}/rules", async (
            int dutyId,
            [FromBody] CreateDutyRuleRequest request,
            IConfigureDutyRulesService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                dutyId, request.TierOrder, request.CriteriaType, request.CriteriaValue, cancellationToken)));

        routes.MapPut("/api/config/2.0/rules/{id:int}", async (
            int id,
            [FromBody] CreateDutyRuleRequest request,
            IConfigureDutyRulesService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateAsync(
                id, request.TierOrder, request.CriteriaType, request.CriteriaValue, cancellationToken)));

        routes.MapDelete("/api/config/2.0/rules/{id:int}", async (
            int id,
            IConfigureDutyRulesService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 3.0 Manage Membership
    // -----------------------------------------------------------------

    private static void MapManageMembership(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/config/3.0/members", async (
            int? branchId,
            bool? includeInactive,
            IManageMemberRecordService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(branchId, includeInactive ?? false, cancellationToken)));

        routes.MapGet("/api/config/3.0/members/{id:int}", async (
            int id,
            IManageMemberRecordService service,
            CancellationToken cancellationToken) =>
        {
            var member = await service.GetAsync(id, cancellationToken);
            return member is null ? Results.NotFound() : Results.Ok(member);
        });

        routes.MapPost("/api/config/3.0/members", async (
            [FromBody] CreateMemberRequest request,
            IManageMemberRecordService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(request, cancellationToken)));

        routes.MapPut("/api/config/3.0/members/{id:int}", async (
            int id,
            [FromBody] UpdateMemberRequest request,
            IManageMemberRecordService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateAsync(id, request, cancellationToken)));

        routes.MapDelete("/api/config/3.0/members/{id:int}", async (
            int id,
            IManageMemberRecordService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));

        // Identifier history
        routes.MapGet("/api/config/3.0/members/{memberId:int}/identifiers", async (
            int memberId,
            IManageIdentifierHistoryService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForMemberAsync(memberId, cancellationToken)));

        routes.MapPost("/api/config/3.0/members/{memberId:int}/identifiers", async (
            int memberId,
            [FromBody] IssueIdentifierRequest request,
            IManageIdentifierHistoryService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.IssueAsync(
                memberId,
                request.Type,
                request.Number,
                DateOnly.Parse(request.AssignedDate),
                request.AuthorizedByAdminId,
                cancellationToken)));

        routes.MapDelete("/api/config/3.0/identifiers/{entryId:int}", async (
            int entryId,
            [FromBody] RetireIdentifierRequest request,
            IManageIdentifierHistoryService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RetireAsync(
                entryId,
                DateOnly.Parse(request.UnassignedDate),
                request.Reason,
                cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 4.0 Manage Eligibility
    // -----------------------------------------------------------------

    private static void MapManageEligibility(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/config/4.0/members/{memberId:int}/eligibility", async (
            int memberId,
            IManageEligibilityService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForMemberAsync(memberId, cancellationToken)));

        routes.MapGet("/api/config/4.0/members/{memberId:int}/eligibility/{dutyId:int}", async (
            int memberId,
            int dutyId,
            IManageEligibilityService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForMemberAndDutyAsync(memberId, dutyId, cancellationToken)));

        routes.MapPost("/api/config/4.0/members/{memberId:int}/eligibility", async (
            int memberId,
            [FromBody] GrantEligibilityRequest request,
            IManageEligibilityService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.GrantAsync(
                memberId,
                request.DutyId,
                DateOnly.Parse(request.GrantedDate),
                request.GrantedByAdminId,
                cancellationToken)));

        routes.MapDelete("/api/config/4.0/eligibility/{eligibilityId:int}", async (
            int eligibilityId,
            [FromBody] RevokeEligibilityRequest request,
            IManageEligibilityService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RevokeAsync(
                eligibilityId,
                DateOnly.Parse(request.RevokedDate),
                request.RevokedReason,
                cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 8.0 Manage Events and Programs
    // -----------------------------------------------------------------

    private static void MapManageEventsAndPrograms(IEndpointRouteBuilder routes)
    {
        // Event
        routes.MapGet("/api/config/8.0/events", async (
            bool? includeInactive,
            IManageEventService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(includeInactive ?? false, cancellationToken)));

        routes.MapPost("/api/config/8.0/events", async (
            [FromBody] CreateEventRequest request,
            IManageEventService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                request.Name,
                DateOnly.Parse(request.StartDate),
                DateOnly.Parse(request.EndDate),
                request.Location,
                request.Type,
                cancellationToken)));

        routes.MapDelete("/api/config/8.0/events/{id:int}", async (
            int id,
            IManageEventService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(id, cancellationToken)));

        // Program
        routes.MapGet("/api/config/8.0/events/{eventId:int}/program", async (
            int eventId,
            IManageProgramService service,
            CancellationToken cancellationToken) =>
        {
            var program = await service.GetForEventAsync(eventId, cancellationToken);
            return program is null ? Results.NotFound() : Results.Ok(program);
        });

        routes.MapPost("/api/config/8.0/events/{eventId:int}/program", async (
            int eventId,
            [FromBody] CreateProgramRequest request,
            IManageProgramService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(eventId, request.Title, cancellationToken)));

        // Program Item
        routes.MapGet("/api/config/8.0/programs/{programId:int}/items", async (
            int programId,
            IManageProgramItemService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForProgramAsync(programId, cancellationToken)));

        routes.MapPost("/api/config/8.0/programs/{programId:int}/items", async (
            int programId,
            [FromBody] CreateProgramItemRequest request,
            IManageProgramItemService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                programId,
                request.SequenceOrder,
                request.Title,
                DateTimeOffset.Parse(request.ScheduledStart),
                DateTimeOffset.Parse(request.ScheduledEnd),
                request.ServiceDefId,
                request.ActingAdminId,
                cancellationToken)));

        // Event Duty
        routes.MapGet("/api/config/8.0/items/{itemId:int}/duties", async (
            int itemId,
            IManageEventDutyService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForItemAsync(itemId, cancellationToken)));

        routes.MapPost("/api/config/8.0/items/{itemId:int}/duties", async (
            int itemId,
            [FromBody] CreateEventDutyRequest request,
            IManageEventDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                itemId, request.Label, request.AssignedMemberId,
                request.AssignmentStatusId, cancellationToken)));

        routes.MapDelete("/api/config/8.0/event-duties/{id:int}", async (
            int id,
            IManageEventDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RemoveAsync(id, cancellationToken)));
    }
}

// -----------------------------------------------------------------
// Request DTOs
// -----------------------------------------------------------------

public sealed record CreateLookupRequest
{
    public required string Name { get; init; }
}

public sealed record CreateNamedEntityRequest
{
    public required string Name { get; init; }
}

public sealed record CreateBranchRequest
{
    public required string Name { get; init; }
    public required string Location { get; init; }
}

public sealed record CreateTimeSlotRequest
{
    public required int BranchId { get; init; }
    public required string DayOfWeek { get; init; }
    public required int TimeOfDayId { get; init; }
}

public sealed record CreateServiceDefinitionRequest
{
    public required string Name { get; init; }
    public required int ServiceTypeId { get; init; }
    public int? OwningBranchId { get; init; }
}

public sealed record AddServiceDutyRequest
{
    public required int DutyId { get; init; }
    public required int RequiredSlotCount { get; init; }
}

public sealed record CreateScheduleRequest
{
    public required int ServiceDefId { get; init; }
    public required int TimeSlotId { get; init; }
    public required string StartTime { get; init; }
}

public sealed record CreateDutyRuleRequest
{
    public required int TierOrder { get; init; }
    public required string CriteriaType { get; init; }
    public required string CriteriaValue { get; init; }
}

public sealed record IssueIdentifierRequest
{
    public required string Type { get; init; }
    public required string Number { get; init; }
    public required string AssignedDate { get; init; }
    public required int AuthorizedByAdminId { get; init; }
}

public sealed record RetireIdentifierRequest
{
    public required string UnassignedDate { get; init; }
    public string? Reason { get; init; }
}

public sealed record GrantEligibilityRequest
{
    public required int DutyId { get; init; }
    public required string GrantedDate { get; init; }
    public required int GrantedByAdminId { get; init; }
}

public sealed record RevokeEligibilityRequest
{
    public required string RevokedDate { get; init; }
    public string? RevokedReason { get; init; }
}

public sealed record CreateEventRequest
{
    public required string Name { get; init; }
    public required string StartDate { get; init; }
    public required string EndDate { get; init; }
    public required string Location { get; init; }
    public required string Type { get; init; }
}

public sealed record CreateProgramRequest
{
    public required string Title { get; init; }
}

public sealed record CreateProgramItemRequest
{
    public required int SequenceOrder { get; init; }
    public required string Title { get; init; }
    public required string ScheduledStart { get; init; }
    public required string ScheduledEnd { get; init; }
    public int? ServiceDefId { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateEventDutyRequest
{
    public required string Label { get; init; }
    public required int AssignedMemberId { get; init; }
    public int? AssignmentStatusId { get; init; }
}
