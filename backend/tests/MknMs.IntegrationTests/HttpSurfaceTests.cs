using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D6_Eligibility;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Exercises every process endpoint over real HTTP, in-process.
/// </summary>
/// <remarks>
/// These tests exist to close the coverage gap that allowed a routing
/// defect to survive: the service-level integration tests call services
/// directly and never trigger ASP.NET Core's endpoint-building path, so
/// a malformed route delegate is invisible to them. This class builds
/// the full host via WebApplicationFactory and issues one HTTP request
/// per process endpoint.
///
/// The class seeds its own state. Every test refers to a specific id —
/// memberId = 1, dutyId = 1, occurrenceId = 1, eligibilityId = 1 — and
/// those ids must resolve. Without a seed, the ids refer to whatever
/// the previous test class left behind, and the tests pass or fail
/// based on execution order. The seed truncates mkn_test and inserts
/// the minimal rows so that each id the tests use is a real row.
///
/// The assertion is deliberately strict: the request must not produce an
/// HTTP 500. A service-level Failure payload, a 4xx, or a 200 are all
/// acceptable — a 500 means the endpoint delegate itself failed, and
/// that is exactly what this class exists to catch.
///
/// Reference: this class was added after the runtime validation that
/// found the missing [FromBody] annotations on ConfigurationEndpoints,
/// and strengthened after a DELETE endpoint returned 500 because its
/// test sent no body.
/// </remarks>
public class HttpSurfaceTests : IClassFixture<WebApplicationFactory<Program>>, IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private readonly WebApplicationFactory<Program> _factory;
    private MknDbContext _db = null!;

    public HttpSurfaceTests(WebApplicationFactory<Program> factory)
    {
        _factory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ConnectionStrings:MknDb", TestConnectionString);
        });
    }

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE materializer_run, attendance_record, roster_assignment, " +
            "service_occurrence_duty, service_occurrence, service_schedule, " +
            "service_definition_duty, branch_time_slot, service_definition, " +
            "service_type, time_of_day, branch, identifier_history, eligibility, " +
            "duty_rule, admin, member, member_attribute_value, member_status, " +
            "attendance_rule_scope, attendance_rule, readmission, role, duty, " +
            "system_setting, outcome_state, assignment_status, permission_tier, " +
            "attribute_type, capability, event_branch, configuration_audit_log, " +
            "entity_deletion_policy, notification_subscription " +
            "RESTART IDENTITY CASCADE;");

        await TestSeed.EnsureRequiredSettingsAsync(_db);

        // Seed the minimal set the tests reference by id.
        await SeedAsync();
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task SeedAsync()
    {
        var role = new Role { Name = "Coordinator", IsActive = true };
        var duty = new Duty { Name = "Welcome duty", IsActive = true };
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var tier = new PermissionTier { Name = "Full Admin" };
        var proposed = new AssignmentStatus { Name = "Proposed", IsTerminal = false };
        var confirmed = new AssignmentStatus { Name = "Confirmed", IsTerminal = false };
        var declined = new AssignmentStatus { Name = "Declined", IsTerminal = true };
        var timedOut = new AssignmentStatus { Name = "Timed Out", IsTerminal = true };
        var unfilled = new OutcomeState { Name = "Unfilled" };
        var partiallyFilled = new OutcomeState { Name = "Partially Filled" };
        var filled = new OutcomeState { Name = "Filled" };

        _db.Roles.Add(role);
        _db.Duties.Add(duty);
        _db.Branches.Add(branch);
        _db.ServiceTypes.Add(serviceType);
        _db.TimeOfDays.Add(timeOfDay);
        _db.PermissionTiers.Add(tier);
        _db.AssignmentStatuses.AddRange(proposed, confirmed, declined, timedOut);
        _db.OutcomeStates.AddRange(unfilled, partiallyFilled, filled);

        await _db.SaveChangesAsync();

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(_db);

        var member = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = "alex@example.com",
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty.DutyId,
            RequiredSlotCount = 1,
        });

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        // An eligibility row with id 1, so the revoke endpoint has a row
        // to act on.
        _db.Eligibilities.Add(new Eligibility
        {
            MemberId = member.MemberId,
            DutyId = duty.DutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = admin.AdminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        await _db.SaveChangesAsync();

        // Settings the processes read.
        // ApplicationTimeZone and AttendanceRegisterEnabled are
        // inserted by TestSeed.EnsureRequiredSettingsAsync in
        // InitializeAsync; do not duplicate them here.
        _db.SystemSettings.AddRange(
            new SystemSetting { Key = "OccurrenceHorizonDays", Value = "30", Required = true },
            new SystemSetting { Key = "InitialAssignmentStatusID", Value = confirmed.AssignmentStatusId.ToString(), Required = true },
            new SystemSetting { Key = "DeclinedStatusID", Value = declined.AssignmentStatusId.ToString(), Required = true },
            new SystemSetting { Key = "TimedOutStatusID", Value = timedOut.AssignmentStatusId.ToString(), Required = true },
            new SystemSetting { Key = "ConfirmationTimeoutHours", Value = "48", Required = true },
            new SystemSetting { Key = "OutcomeStateUnfilledID", Value = unfilled.OutcomeStateId.ToString(), Required = true },
            new SystemSetting { Key = "OutcomeStatePartiallyFilledID", Value = partiallyFilled.OutcomeStateId.ToString(), Required = true },
            new SystemSetting { Key = "OutcomeStateFilledID", Value = filled.OutcomeStateId.ToString(), Required = true },
            new SystemSetting { Key = "NotificationChannel", Value = "Log", Required = false });
        await _db.SaveChangesAsync();

        // Additional entities the soft-delete smoke tests exercise.
        // Each is chosen so its cascade either succeeds or blocks as
        // the test intends.

        // A default Role, so the smoke soft delete of a non-default
        // Role has a fallback target.
        _db.Roles.Add(new Role { Name = "SmokeDefault", IsDefault = true });
        // A non-default Role with no member and no rule referring to
        // it — the successful case.
        _db.Roles.Add(new Role { Name = "SmokeRole" });

        // A second Branch with no Member assigned — the successful
        // Branch soft delete case.
        _db.Branches.Add(new Branch
        {
            Name = "SmokeBranch", Location = "Nowhere",
        });

        // An OutcomeState not referenced by any setting or occurrence.
        _db.OutcomeStates.Add(new OutcomeState { Name = "SmokeOutcomeState" });

        // An Event with no Programs — the successful Event cascade.
        _db.Events.Add(new MknMs.Domain.D9_EventProgram.Event
        {
            Name = "SmokeEvent",
            StartDate = new DateOnly(2026, 12, 1),
            EndDate = new DateOnly(2026, 12, 2),
            Type = "Conference",
            HostBranchId = branch.BranchId,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>
    /// The request must not produce a 500. A 500 is the framework or
    /// the endpoint delegate failing before or during execution; it is
    /// never an acceptable outcome for a smoke test whose whole purpose
    /// is that the endpoint was reached.
    /// </summary>
    private static async Task AssertNoServerErrorAsync(HttpResponseMessage response, string endpoint)
    {
        if (response.StatusCode == HttpStatusCode.InternalServerError)
        {
            var body = await response.Content.ReadAsStringAsync();
            body.Should().BeEmpty(
                $"endpoint {endpoint} must not return 500. Body was: {body}");
        }
    }

    [Fact]
    public async Task Root_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task GetRoles_ReturnsOk()
    {
        var client = _factory.CreateClient();
        var response = await client.GetAsync("/api/config/1.0/roles");
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Materialize_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/11.0/materialize",
            new { triggerType = "Scheduled" });

        await AssertNoServerErrorAsync(response, "/api/processes/11.0/materialize");
    }

    [Fact]
    public async Task GenerateAssignment_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/5.0/generate",
            new { fromDate = "2026-10-06", toDate = "2026-10-31" });

        await AssertNoServerErrorAsync(response, "/api/processes/5.0/generate");
    }

    [Fact]
    public async Task ManageConfirmationRespond_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/7.0/respond",
            new { assignmentId = 1, operation = "Confirm" });

        await AssertNoServerErrorAsync(response, "/api/processes/7.0/respond");
    }

    [Fact]
    public async Task ManageConfirmationSweep_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/7.0/sweep",
            new { });

        await AssertNoServerErrorAsync(response, "/api/processes/7.0/sweep");
    }

    [Fact]
    public async Task EvaluateFillStatus_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/10.0/evaluate",
            new { fromDate = "2026-10-06", toDate = "2026-10-31" });

        await AssertNoServerErrorAsync(response, "/api/processes/10.0/evaluate");
    }

    [Fact]
    public async Task RecordAttendance_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/6.0/record",
            new { memberId = 1, occurrenceId = 1 });

        await AssertNoServerErrorAsync(response, "/api/processes/6.0/record");
    }

    [Fact]
    public async Task CreateManualAssignment_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/processes/12.0/manual",
            new { memberId = 1, dutyId = 1, occurrenceId = 1, actingAdminId = 1 });

        await AssertNoServerErrorAsync(response, "/api/processes/12.0/manual");
    }

    [Fact]
    public async Task IssueIdentifier_WithRouteAndBody_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/config/3.0/members/1/identifiers",
            new
            {
                type = "Card",
                number = "HTTP-TEST-001",
                assignedDate = "2026-10-06",
                authorizedByAdminId = 1,
            });

        await AssertNoServerErrorAsync(response, "/api/config/3.0/members/1/identifiers");
    }

    [Fact]
    public async Task GrantEligibility_WithRouteAndBody_AcceptsRequest()
    {
        var client = _factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/config/4.0/members/1/eligibility",
            new { dutyId = 1, grantedDate = "2026-10-06", grantedByAdminId = 1 });

        await AssertNoServerErrorAsync(response, "/api/config/4.0/members/1/eligibility");
    }

    [Fact]
    public async Task SoftDeleteRole_ReachesEndpoint()
    {
        var client = _factory.CreateClient();

        var roleId = await _db.Roles
            .Where(r => r.Name == "SmokeRole")
            .Select(r => r.RoleId)
            .FirstAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/config/1.0/roles/{roleId}/soft-delete",
            new
            {
                actingAdminId = 1,
                approvedByAdminId = 1,
                consequencesAcknowledged = true,
                reason = "smoke test",
            });

        await AssertNoServerErrorAsync(response, $"roles/{roleId}/soft-delete");
    }

    [Fact]
    public async Task SoftDeleteBranch_ReachesEndpoint()
    {
        var client = _factory.CreateClient();

        var branchId = await _db.Branches
            .Where(b => b.Name == "SmokeBranch")
            .Select(b => b.BranchId)
            .FirstAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/config/1.0/branches/{branchId}/soft-delete",
            new
            {
                actingAdminId = 1,
                approvedByAdminId = 1,
                consequencesAcknowledged = true,
                reason = "smoke test",
            });

        await AssertNoServerErrorAsync(response, $"branches/{branchId}/soft-delete");
    }

    [Fact]
    public async Task SoftDeleteOutcomeState_ReachesEndpoint()
    {
        var client = _factory.CreateClient();

        var stateId = await _db.OutcomeStates
            .Where(s => s.Name == "SmokeOutcomeState")
            .Select(s => s.OutcomeStateId)
            .FirstAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/config/3.6/outcome-states/{stateId}/soft-delete",
            new
            {
                actingAdminId = 1,
                approvedByAdminId = 1,
                consequencesAcknowledged = true,
                reason = "smoke test",
            });

        await AssertNoServerErrorAsync(response, $"outcome-states/{stateId}/soft-delete");
    }

    [Fact]
    public async Task SoftDeleteEvent_ReachesEndpoint()
    {
        var client = _factory.CreateClient();

        var eventId = await _db.Events
            .Where(e => e.Name == "SmokeEvent")
            .Select(e => e.EventId)
            .FirstAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/config/8.0/events/{eventId}/soft-delete",
            new
            {
                actingAdminId = 1,
                approvedByAdminId = 1,
                consequencesAcknowledged = true,
                reason = "smoke test",
            });

        await AssertNoServerErrorAsync(response, $"events/{eventId}/soft-delete");
    }

    [Fact]
    public async Task SoftDeleteBranch_BlockedByMember_NoServerError()
    {
        // The seeded "Main" branch has Alex assigned. The cascade
        // blocks; the endpoint must still return a non-500 response.
        var client = _factory.CreateClient();

        var branchId = await _db.Branches
            .Where(b => b.Name == "Main")
            .Select(b => b.BranchId)
            .FirstAsync();

        var response = await client.PostAsJsonAsync(
            $"/api/config/1.0/branches/{branchId}/soft-delete",
            new
            {
                actingAdminId = 1,
                approvedByAdminId = 1,
                consequencesAcknowledged = true,
                reason = "should be blocked",
            });

        await AssertNoServerErrorAsync(response, $"branches/{branchId}/soft-delete (blocked)");
    }

    [Fact]
    public async Task RevokeEligibility_WithRouteAndBody_AcceptsRequest()
    {
        var client = _factory.CreateClient();

        // DELETE with a body: the endpoint requires a
        // RevokeEligibilityRequest payload, and the request must carry
        // it. The row with id 1 is seeded in InitializeAsync.
        var request = new HttpRequestMessage(HttpMethod.Delete,
            "/api/config/4.0/eligibility/1")
        {
            Content = JsonContent.Create(new
            {
                revokedDate = "2026-10-06",
                revokedReason = "http surface test",
            }),
        };

        var response = await client.SendAsync(request);

        await AssertNoServerErrorAsync(response, "/api/config/4.0/eligibility/1");
    }
}
