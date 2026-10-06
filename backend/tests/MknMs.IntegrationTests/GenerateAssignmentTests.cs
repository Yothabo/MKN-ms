using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Application.Processes.Operations.GenerateAssignment;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D6_Eligibility;
using MknMs.Domain.D7_RosterAssignment;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 5.0 Generate Assignment.
/// </summary>
/// <remarks>
/// Runs against a real PostgreSQL database named mkn_test.
///
/// Verifies:
///   - Duty Rule ranking + Eligibility gate both participate.
///   - A duty with eligible members but no matching tier remains
///     unfilled.
///   - A duty with an eligible member matching its tier is filled.
///   - Inserted assignments carry the correct provenance fields.
///   - Idempotency: a second run creates no additional rows.
///
/// Reference: docs/processes/operations/5.0-generate-assignment.md,
/// Specification §5 and §10.
/// </remarks>
public class GenerateAssignmentTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private MknDbContext _db = null!;
    private IDispatchNotificationService _notification = null!;
    private TimeProvider _clock = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);
        _notification = new FakeDispatchNotificationService();
        // superseded: NullLogger no longer needed
        _clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE materializer_run, attendance_record, roster_assignment, " +
            "service_occurrence_duty, service_occurrence, service_schedule, " +
            "service_definition_duty, branch_time_slot, service_definition, " +
            "service_type, time_of_day, branch, identifier_history, eligibility, " +
            "duty_rule, admin, member, role, duty, system_setting, " +
            "outcome_state, assignment_status, permission_tier RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    [Fact]
    public async Task Run_WithMixedEligibilityAndTier_ProducesExpectedOutcome()
    {
        var fixture = await SeedFixtureAsync();
        var service = new GenerateAssignmentService(_db, _notification, _clock);

        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesProcessed.Should().Be(1);

        // Reading duty — Beatrice is eligible and matches Tier 1
        // (Role=Steward). One assignment expected.
        // Welcome duty — Dana is the only eligible member, but Dana is
        // a Steward and the Tier 1 rule requires Coordinator. No tier
        // matches. No assignment.
        result.AssignmentsCreated.Should().Be(1);
        result.DutiesFullyFilled.Should().Be(1);
        result.DutiesUnfilled.Should().Be(1);
        result.DutiesPartiallyFilled.Should().Be(0);

        var assignments = await _db.RosterAssignments.ToListAsync();
        assignments.Should().HaveCount(1);

        var reading = assignments.Single();
        reading.DutyId.Should().Be(fixture.ReadingDutyId);
        reading.MemberId.Should().Be(fixture.BeatriceMemberId);
        reading.OccurrenceId.Should().Be(fixture.OccurrenceId);
        reading.AssignmentStatusId.Should().BeNull();
        reading.AssignmentSource.Should().Be("Automatic");
        reading.AssignedBy.Should().BeNull();
        reading.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task Run_Twice_IsIdempotent()
    {
        var fixture = await SeedFixtureAsync();
        var service = new GenerateAssignmentService(_db, _notification, _clock);

        var first = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        var countAfterFirst = await _db.RosterAssignments.CountAsync();

        var second = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        var countAfterSecond = await _db.RosterAssignments.CountAsync();

        first.AssignmentsCreated.Should().Be(1);
        second.AssignmentsCreated.Should().Be(0);
        countAfterSecond.Should().Be(countAfterFirst);
    }

    [Fact]
    public async Task Run_WithNoEligibility_ProducesNoAssignments()
    {
        var fixture = await SeedFixtureAsync(includeEligibility: false);
        var service = new GenerateAssignmentService(_db, _notification, _clock);

        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = fixture.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsCreated.Should().Be(0);

        var assignments = await _db.RosterAssignments.CountAsync();
        assignments.Should().Be(0);
    }

    [Fact]
    public async Task Run_WithInvalidCommand_ReturnsFailure()
    {
        await SeedFixtureAsync();
        var service = new GenerateAssignmentService(_db, _notification, _clock);

        // Neither occurrence nor range supplied.
        var result = await service.RunAsync(new GenerateAssignmentCommand());

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Exactly one");
    }

    // -----------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------

    private sealed record Fixture(
        int OccurrenceId,
        int ReadingDutyId,
        int BeatriceMemberId);

    private async Task<Fixture> SeedFixtureAsync(bool includeEligibility = true)
    {
        // Lookups
        var outcomeState = new OutcomeState { Name = "Unfilled" };
        _db.OutcomeStates.Add(outcomeState);

        var serviceType = new ServiceType { Name = "Regular" };
        _db.ServiceTypes.Add(serviceType);

        var timeOfDay = new TimeOfDay { Name = "Morning" };
        _db.TimeOfDays.Add(timeOfDay);

        // Roles
        var coordinator = new Role { Name = "Coordinator", IsActive = true };
        var steward = new Role { Name = "Steward", IsActive = true };
        _db.Roles.Add(coordinator);
        _db.Roles.Add(steward);

        // Duties
        var welcomeDuty = new Duty { Name = "Welcome duty", IsActive = true };
        var readingDuty = new Duty { Name = "Reading duty", IsActive = true };
        _db.Duties.Add(welcomeDuty);
        _db.Duties.Add(readingDuty);

        // Branch
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        _db.Branches.Add(branch);

        // Permission tier + admin
        var tier = new PermissionTier { Name = "Full Admin" };
        _db.PermissionTiers.Add(tier);

        await _db.SaveChangesAsync();

        // Members
        var alex = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = coordinator.RoleId,
            IsActive = true,
        };
        var beatrice = new Member
        {
            Name = "Beatrice", Surname = "Sample",
            JoinDate = new DateOnly(2024, 6, 1),
            DateOfBirth = new DateOnly(1995, 3, 12),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0001", Email = null,
            BranchId = branch.BranchId, RoleId = steward.RoleId,
            IsActive = true,
        };
        var dana = new Member
        {
            Name = "Dana", Surname = "Prototype",
            JoinDate = new DateOnly(2023, 4, 20),
            DateOfBirth = new DateOnly(1985, 11, 30),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0003", Email = null,
            BranchId = branch.BranchId, RoleId = steward.RoleId,
            IsActive = true,
        };
        _db.Members.Add(alex);
        _db.Members.Add(beatrice);
        _db.Members.Add(dana);

        await _db.SaveChangesAsync();

        // Admin row for Alex.
        var admin = new Admin
        {
            MemberId = alex.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        // Time slot, service definition, schedule.
        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsActive = true,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = true,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        // Attach duties to the definition.
        _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = welcomeDuty.DutyId,
            RequiredSlotCount = 1,
            IsActive = true,
        });
        _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = readingDuty.DutyId,
            RequiredSlotCount = 1,
            IsActive = true,
        });

        // The schedule.
        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsActive = true,
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        // Duty rules.
        _db.DutyRules.Add(new DutyRule
        {
            DutyId = welcomeDuty.DutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Coordinator",
            IsActive = true,
        });
        _db.DutyRules.Add(new DutyRule
        {
            DutyId = readingDuty.DutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Steward",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        // Eligibility.
        if (includeEligibility)
        {
            _db.Eligibilities.Add(new Eligibility
            {
                MemberId = dana.MemberId,
                DutyId = welcomeDuty.DutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = admin.AdminId,
                RevokedDate = null,
                RevokedReason = null,
            });
            _db.Eligibilities.Add(new Eligibility
            {
                MemberId = beatrice.MemberId,
                DutyId = readingDuty.DutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = admin.AdminId,
                RevokedDate = null,
                RevokedReason = null,
            });
            await _db.SaveChangesAsync();
        }

        // The occurrence to fill.
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

        return new Fixture(
            occurrence.OccurrenceId,
            readingDuty.DutyId,
            beatrice.MemberId);
    }
}
