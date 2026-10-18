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
using MknMs.Application.Processes.Configuration.Lookup_Capability;
using MknMs.Application.Processes.Configuration.Lookup_TimeOfDay;
using MknMs.Application.Processes.Configuration.ManageAttendanceRule;
using MknMs.Application.Processes.Configuration.ManageEligibility;
using MknMs.Application.Processes.Configuration.ManageEvent;
using MknMs.Application.Processes.Configuration.ManageEventDuty;
using MknMs.Application.Processes.Configuration.ManageIdentifierHistory;
using MknMs.Application.Processes.Configuration.ManageMemberRecord;
using MknMs.Application.Processes.Configuration.ManageAttributeType;
using MknMs.Application.Processes.Configuration.ManageLookups;
using MknMs.Application.Processes.Configuration.ManageMemberStatus;
using MknMs.Application.Processes.Configuration.RecordReadmission;
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
        MapManageAttendanceRule(routes);
        MapManageMemberStatus(routes);
        MapRecordReadmission(routes);
        MapManageAttributeType(routes);
        MapSetMemberAttribute(routes);
        MapManageLookups(routes);
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
            ConfigurationErrorCodes.LifecycleViolation =>
                Results.UnprocessableEntity(new { error = result.Error }),
            ConfigurationErrorCodes.ValidationFailed =>
                Results.BadRequest(new { error = result.Error }),
            ConfigurationErrorCodes.ReferencedEntityMissing or
            ConfigurationErrorCodes.ReferencedEntityInactive or
            ConfigurationErrorCodes.InvalidValue =>
                Results.UnprocessableEntity(new { error = result.Error }),
            _ =>
                Results.BadRequest(new { error = result.Error }),
        };
    }

    // Request-boundary parsing. Malformed input is a caller error, not
    // a server fault; the adapter contract requires it to surface as
    // the documented validation response rather than as an unhandled
    // exception. Each helper returns false and populates an IResult
    // carrying the standard BadRequest shape when the field cannot be
    // parsed.

    private static bool TryParseIsoDate(
        string? value, string fieldName, out DateOnly result, out IResult error)
    {
        if (DateOnly.TryParse(value, out result))
        {
            error = null!;
            return true;
        }
        error = Results.BadRequest(new
        {
            error = new ConfigurationError(
                ConfigurationErrorCodes.ValidationFailed,
                $"{fieldName} must be an ISO 8601 date (yyyy-MM-dd)."),
        });
        return false;
    }

    private static bool TryParseTimestamp(
        string? value, string fieldName, out DateTimeOffset result, out IResult error)
    {
        if (DateTimeOffset.TryParse(value, out result))
        {
            error = null!;
            return true;
        }
        error = Results.BadRequest(new
        {
            error = new ConfigurationError(
                ConfigurationErrorCodes.ValidationFailed,
                $"{fieldName} must be an ISO 8601 timestamp."),
        });
        return false;
    }

    private static bool TryParseTimeOfDay(
        string? value, string fieldName, out TimeOnly result, out IResult error)
    {
        if (TimeOnly.TryParse(value, out result))
        {
            error = null!;
            return true;
        }
        error = Results.BadRequest(new
        {
            error = new ConfigurationError(
                ConfigurationErrorCodes.ValidationFailed,
                $"{fieldName} must be a time (HH:mm or HH:mm:ss)."),
        });
        return false;
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

        routes.MapGet("/api/lookups/capabilities", async (
            ICapabilityService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        routes.MapPost("/api/lookups/capabilities", async (
            [FromBody] CreateCapabilityRequest request,
            ICapabilityService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                request.Name, request.Description, cancellationToken)));
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
            ToHttpResult(await service.CreateAsync(request.Name, request.ActingAdminId, cancellationToken)));

        routes.MapPut("/api/config/1.0/roles/{id:int}", async (
            int id,
            [FromBody] CreateNamedEntityRequest request,
            IConfigureRoleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RenameAsync(id, request.Name, request.ActingAdminId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/roles/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureRoleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/1.0/roles/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureRoleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

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
            ToHttpResult(await service.CreateAsync(request.Name, request.ActingAdminId, cancellationToken)));

        routes.MapPut("/api/config/1.0/duties/{id:int}", async (
            int id,
            [FromBody] CreateNamedEntityRequest request,
            IConfigureDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RenameAsync(id, request.Name, request.ActingAdminId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/duties/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/1.0/duties/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

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
            ToHttpResult(await service.CreateAsync(request.Name, request.Location, request.ActingAdminId, cancellationToken)));

        routes.MapPut("/api/config/1.0/branches/{id:int}", async (
            int id,
            [FromBody] CreateBranchRequest request,
            IConfigureBranchService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateAsync(id, request.Name, request.Location, request.ActingAdminId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/branches/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureBranchService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/1.0/branches/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureBranchService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

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
                request.BranchId, request.DayOfWeek, request.TimeOfDayId,
                request.ActingAdminId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/time-slots/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureTimeSlotService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/1.0/time-slots/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureTimeSlotService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

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
                request.Name, request.ServiceTypeId, request.ActingAdminId,
                request.OwningBranchId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/service-definitions/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureServiceDefinitionService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/1.0/service-definitions/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureServiceDefinitionService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

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
            ToHttpResult(await service.AddDutyAsync(id, request.DutyId, request.RequiredSlotCount, request.ActingAdminId, cancellationToken)));

        routes.MapDelete("/api/config/1.0/service-definitions/{id:int}/duties/{dutyId:int}", async (
            int id,
            int dutyId,
            [FromBody] LifecycleActionRequest request,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RemoveDutyAsync(
                id, dutyId, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

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
        {
            if (!TryParseTimeOfDay(request.StartTime, "StartTime", out var startTime, out var startTimeError))
            {
                return startTimeError;
            }
            return ToHttpResult(await service.CreateScheduleAsync(
                request.ServiceDefId, request.TimeSlotId,
                startTime, request.ActingAdminId,
                cancellationToken));
        });

        routes.MapDelete("/api/config/1.0/service-schedules/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateScheduleAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/1.0/service-schedules/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureServiceDutyAndScheduleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteScheduleAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));
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
                dutyId, request.TierOrder, request.CriteriaType, request.CriteriaValue,
                request.ActingAdminId, cancellationToken)));

        routes.MapPut("/api/config/2.0/rules/{id:int}", async (
            int id,
            [FromBody] CreateDutyRuleRequest request,
            IConfigureDutyRulesService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateAsync(
                id, request.TierOrder, request.CriteriaType, request.CriteriaValue,
                request.ActingAdminId, cancellationToken)));

        routes.MapDelete("/api/config/2.0/rules/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IConfigureDutyRulesService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));
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

        routes.MapPost("/api/config/3.0/members/{id:int}/set-status", async (
            int id,
            [FromBody] SetMemberStatusRequest request,
            IManageMemberRecordService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SetStatusAsync(id, request.MemberStatusId, cancellationToken)));

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
        {
            if (!TryParseIsoDate(request.AssignedDate, "AssignedDate", out var assignedDate, out var assignedError))
            {
                return assignedError;
            }
            return ToHttpResult(await service.IssueAsync(
                memberId,
                request.Type,
                request.Number,
                assignedDate,
                request.AuthorizedByAdminId,
                cancellationToken));
        });

        routes.MapDelete("/api/config/3.0/identifiers/{entryId:int}", async (
            int entryId,
            [FromBody] RetireIdentifierRequest request,
            IManageIdentifierHistoryService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseIsoDate(request.UnassignedDate, "UnassignedDate", out var unassignedDate, out var unassignedError))
            {
                return unassignedError;
            }
            return ToHttpResult(await service.RetireAsync(
                entryId,
                unassignedDate,
                request.Reason,
                cancellationToken));
        });
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
        {
            if (!TryParseIsoDate(request.GrantedDate, "GrantedDate", out var grantedDate, out var grantedError))
            {
                return grantedError;
            }
            return ToHttpResult(await service.GrantAsync(
                memberId,
                request.DutyId,
                grantedDate,
                request.GrantedByAdminId,
                cancellationToken));
        });

        routes.MapDelete("/api/config/4.0/eligibility/{eligibilityId:int}", async (
            int eligibilityId,
            [FromBody] RevokeEligibilityRequest request,
            IManageEligibilityService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseIsoDate(request.RevokedDate, "RevokedDate", out var revokedDate, out var revokedError))
            {
                return revokedError;
            }
            return ToHttpResult(await service.RevokeAsync(
                eligibilityId,
                revokedDate,
                request.RevokedReason,
                cancellationToken));
        });
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
        {
            if (!TryParseIsoDate(request.StartDate, "StartDate", out var startDate, out var startErr))
            {
                return startErr;
            }
            if (!TryParseIsoDate(request.EndDate, "EndDate", out var endDate, out var endErr))
            {
                return endErr;
            }
            return ToHttpResult(await service.CreateAsync(
                request.Name,
                startDate,
                endDate,
                request.Type,
                request.HostBranchId,
                request.AttendingBranchIds,
                request.UsesAttendanceRegister,
                cancellationToken));
        });

        routes.MapDelete("/api/config/8.0/events/{id:int}", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageEventService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/8.0/events/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageEventService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

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
        {
            if (!TryParseTimestamp(request.ScheduledStart, "ScheduledStart", out var sStart, out var sStartErr))
            {
                return sStartErr;
            }
            if (!TryParseTimestamp(request.ScheduledEnd, "ScheduledEnd", out var sEnd, out var sEndErr))
            {
                return sEndErr;
            }
            return ToHttpResult(await service.CreateAsync(
                programId,
                request.SequenceOrder,
                request.Title,
                sStart,
                sEnd,
                request.ServiceDefId,
                request.ActingAdminId,
                cancellationToken));
        });

        // Event Duty
        routes.MapGet("/api/config/8.0/events/{eventId:int}/duties", async (
            int eventId,
            IManageEventDutyService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListForEventAsync(eventId, cancellationToken)));

        routes.MapPost("/api/config/8.0/events/{eventId:int}/duties", async (
            int eventId,
            [FromBody] CreateEventDutyRequest request,
            IManageEventDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                eventId, request.DutyId, request.ServiceDefId,
                request.RequiredSlotCount,
                cancellationToken)));

        routes.MapDelete("/api/config/8.0/event-duties/{id:int}", async (
            int id,
            IManageEventDutyService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.RemoveAsync(id, cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 3.4 Manage Member Status
    // -----------------------------------------------------------------

    // -----------------------------------------------------------------
    // 3.7 Record Readmission
    // -----------------------------------------------------------------

    private static void MapRecordReadmission(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/config/3.0/members/{memberId:int}/readmissions", async (
            int memberId,
            [FromBody] RecordReadmissionRequest request,
            IRecordReadmissionService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseIsoDate(request.ReadmissionDate, "ReadmissionDate", out var readmissionDate, out var dateError))
            {
                return dateError;
            }

            return ToHttpResult(await service.RecordAsync(
                new RecordReadmissionCommand
                {
                    MemberId = memberId,
                    ReadmissionDate = readmissionDate,
                    Reason = request.Reason,
                    ActingAdminId = request.ActingAdminId,
                },
                cancellationToken));
        });
    }

    private static void MapManageMemberStatus(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/config/3.4/member-statuses/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageMemberStatusService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 3.6 Manage D10 Lookups
    // -----------------------------------------------------------------

    private static void MapManageLookups(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/config/3.6/outcome-states/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageLookupsService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteOutcomeStateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

        routes.MapPost("/api/config/3.6/assignment-statuses/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageLookupsService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAssignmentStatusAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

        routes.MapPost("/api/config/3.6/time-of-days/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageLookupsService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteTimeOfDayAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

        routes.MapPost("/api/config/3.6/service-types/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageLookupsService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteServiceTypeAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));

        routes.MapPost("/api/config/3.6/permission-tiers/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageLookupsService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeletePermissionTierAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 3.5 Manage Attribute Type
    // -----------------------------------------------------------------

    private static void MapManageAttributeType(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/config/3.5/attribute-types", async (
            IManageAttributeTypeService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(cancellationToken)));

        routes.MapPost("/api/config/3.5/attribute-types", async (
            [FromBody] CreateAttributeTypeRequest request,
            IManageAttributeTypeService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(
                request.Name, request.Description,
                request.ActingAdminId, cancellationToken)));

        routes.MapPost("/api/config/3.5/attribute-types/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageAttributeTypeService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));
    }

    // -----------------------------------------------------------------
    // 3.1 Set Member Attribute
    // -----------------------------------------------------------------

    private static void MapSetMemberAttribute(IEndpointRouteBuilder routes)
    {
        routes.MapPost("/api/config/3.0/members/{memberId:int}/attributes", async (
            int memberId,
            [FromBody] SetMemberAttributeRequest request,
            ISetMemberAttributeService service,
            CancellationToken cancellationToken) =>
        {
            if (!TryParseIsoDate(request.RecordedDate, "RecordedDate", out var recordedDate, out var dateError))
            {
                return dateError;
            }

            return ToHttpResult(await service.SetAsync(
                new SetMemberAttributeCommand
                {
                    MemberId = memberId,
                    AttributeTypeId = request.AttributeTypeId,
                    Value = request.Value,
                    RecordedDate = recordedDate,
                    ActingAdminId = request.ActingAdminId,
                },
                cancellationToken));
        });
    }

    // -----------------------------------------------------------------
    // 3.3 Manage Attendance Rule
    // -----------------------------------------------------------------

    private static void MapManageAttendanceRule(IEndpointRouteBuilder routes)
    {
        routes.MapGet("/api/config/3.3/attendance-rules", async (
            bool? includeDisabled,
            bool? includeInactive,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListAsync(
                includeDisabled ?? true,
                includeInactive ?? false,
                cancellationToken)));

        routes.MapGet("/api/config/3.3/attendance-rules/{id:int}", async (
            int id,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
        {
            var rule = await service.GetAsync(id, cancellationToken);
            return rule is null ? Results.NotFound() : Results.Ok(rule);
        });

        routes.MapGet("/api/config/3.3/attendance-rules/{id:int}/scope", async (
            int id,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            Results.Ok(await service.ListScopeAsync(id, cancellationToken)));

        routes.MapPost("/api/config/3.3/attendance-rules", async (
            [FromBody] CreateAttendanceRuleRequest request,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.CreateAsync(request, cancellationToken)));

        routes.MapPut("/api/config/3.3/attendance-rules/{id:int}", async (
            int id,
            [FromBody] UpdateAttendanceRuleRequest request,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.UpdateAsync(id, request, cancellationToken)));

        routes.MapPost("/api/config/3.3/attendance-rules/{id:int}/disable", async (
            int id,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DisableAsync(id, cancellationToken)));

        routes.MapPost("/api/config/3.3/attendance-rules/{id:int}/enable", async (
            int id,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.EnableAsync(id, cancellationToken)));

        routes.MapPost("/api/config/3.3/attendance-rules/{id:int}/deactivate", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.DeactivateAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason,
                cancellationToken)));

        routes.MapPost("/api/config/3.3/attendance-rules/{id:int}/soft-delete", async (
            int id,
            [FromBody] LifecycleActionRequest request,
            IManageAttendanceRuleService service,
            CancellationToken cancellationToken) =>
            ToHttpResult(await service.SoftDeleteAsync(
                id, request.ActingAdminId, request.ApprovedByAdminId,
                request.ConsequencesAcknowledged, request.Reason ?? string.Empty,
                cancellationToken)));
    }
}

// -----------------------------------------------------------------
// Request DTOs
// -----------------------------------------------------------------

public sealed record CreateLookupRequest
{
    public required string Name { get; init; }
}

public sealed record CreateAttributeTypeRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record SetMemberAttributeRequest
{
    public required int AttributeTypeId { get; init; }
    public required string Value { get; init; }
    public required string RecordedDate { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateCapabilityRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}

public sealed record CreateNamedEntityRequest
{
    public required string Name { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateBranchRequest
{
    public required string Name { get; init; }
    public required string Location { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateTimeSlotRequest
{
    public required int BranchId { get; init; }
    public required string DayOfWeek { get; init; }
    public required int TimeOfDayId { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateServiceDefinitionRequest
{
    public required string Name { get; init; }
    public required int ServiceTypeId { get; init; }
    public int? OwningBranchId { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record AddServiceDutyRequest
{
    public required int DutyId { get; init; }
    public required int RequiredSlotCount { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateScheduleRequest
{
    public required int ServiceDefId { get; init; }
    public required int TimeSlotId { get; init; }
    public required string StartTime { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateDutyRuleRequest
{
    public required int TierOrder { get; init; }
    public required string CriteriaType { get; init; }
    public required string CriteriaValue { get; init; }
    public required int ActingAdminId { get; init; }
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
    public required string Type { get; init; }
    public required int HostBranchId { get; init; }
    public IReadOnlyList<int>? AttendingBranchIds { get; init; }
    public bool? UsesAttendanceRegister { get; init; }
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

public sealed record ActingAdminRequest
{
    public required int ActingAdminId { get; init; }
}

public sealed record LifecycleActionRequest
{
    public required int ActingAdminId { get; init; }
    public required int ApprovedByAdminId { get; init; }
    public required bool ConsequencesAcknowledged { get; init; }
    public string? Reason { get; init; }
}

public sealed record SetMemberStatusRequest
{
    public required int MemberStatusId { get; init; }
}

public sealed record RecordReadmissionRequest
{
    public required string ReadmissionDate { get; init; }
    public string? Reason { get; init; }
    public required int ActingAdminId { get; init; }
}

public sealed record CreateEventDutyRequest
{
    public required int DutyId { get; init; }
    public int? ServiceDefId { get; init; }
    public required int RequiredSlotCount { get; init; }
}
