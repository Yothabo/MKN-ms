using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Application.Processes.Operations.ManageConfirmation;
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
/// Integration tests for process 7.0 Manage Confirmation.
/// </summary>
/// <remarks>
/// Runs against mkn_test. Exercises:
///   - The required-setting refusal on InitialAssignmentStatusID.
///   - Status transitions with a non-terminal target (no re-resolve).
///   - Status transitions with a terminal target (re-resolve).
///   - Replacement creation with the correct provenance fields.
///   - Cumulative exclusion of members holding a terminal assignment
///     on the affected slot.
///   - Timeout sweep: the rule now - CreatedAt >= ConfirmationTimeoutHours.
///   - Sweep with the required setting missing.
///   - Invalid command shape.
///
/// Reference: docs/processes/operations/7.0-manage-confirmation.md,
/// Specification §11.
/// </remarks>
public class ManageConfirmationTests : IAsyncLifetime
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

    // -----------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------

    private sealed record Fixture(
        int OccurrenceId,
        int ReadingDutyId,
        int AssignmentId,
        int BeatriceMemberId,
        int DanaMemberId,
        int ProposedStatusId,
        int ConfirmedStatusId,
        int DeclinedStatusId);

    private async Task<Fixture> SeedFixtureAsync(
        bool includeInitialStatusSetting = true,
        bool includeTimeoutSetting = true)
    {
        // Lookups.
        _db.OutcomeStates.Add(new OutcomeState { Name = "Unfilled" });
        _db.ServiceTypes.Add(new ServiceType { Name = "Regular" });
        _db.TimeOfDays.Add(new TimeOfDay { Name = "Morning" });

        var coordinator = new Role { Name = "Coordinator", IsActive = true };
        var steward = new Role { Name = "Steward", IsActive = true };
        _db.Roles.Add(coordinator);
        _db.Roles.Add(steward);

        var welcomeDuty = new Duty { Name = "Welcome duty", IsActive = true };
        var readingDuty = new Duty { Name = "Reading duty", IsActive = true };
        _db.Duties.Add(welcomeDuty);
        _db.Duties.Add(readingDuty);

        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        _db.Branches.Add(branch);

        var tier = new PermissionTier { Name = "Full Admin" };
        _db.PermissionTiers.Add(tier);

        // Assignment statuses. Proposed = non-terminal, Confirmed =
        // non-terminal (an admin may mark a confirmation either way),
        // Declined = terminal, Timed Out = terminal.
        var proposed = new AssignmentStatus { Name = "Proposed", IsTerminal = false };
        var confirmed = new AssignmentStatus { Name = "Confirmed", IsTerminal = false };
        var declined = new AssignmentStatus { Name = "Declined", IsTerminal = true };
        var timedOut = new AssignmentStatus { Name = "Timed Out", IsTerminal = true };
        _db.AssignmentStatuses.Add(proposed);
        _db.AssignmentStatuses.Add(confirmed);
        _db.AssignmentStatuses.Add(declined);
        _db.AssignmentStatuses.Add(timedOut);

        if (includeInitialStatusSetting)
        {
            // Placeholder value; populated with the real id after
            // SaveChanges so it can reference the confirmed status.
        }

        await _db.SaveChangesAsync();

        if (includeInitialStatusSetting)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "InitialAssignmentStatusID",
                Value = confirmed.AssignmentStatusId.ToString(),
                Required = true,
            });
        }

        if (includeTimeoutSetting)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "ConfirmationTimeoutHours",
                Value = "48",
                Required = true,
            });
        }

        await _db.SaveChangesAsync();

        // Members.
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

        var admin = new Admin
        {
            MemberId = alex.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = _db.TimeOfDays.Local.First().TimeOfDayId,
            IsActive = true,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = _db.ServiceTypes.Local.First().ServiceTypeId,
            OwningBranchId = null,
            IsActive = true,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

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
        await _db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsActive = true,
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        // Duty rules: Reading duty prefers Steward.
        _db.DutyRules.Add(new DutyRule
        {
            DutyId = readingDuty.DutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Steward",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        // Eligibility: Beatrice and Dana eligible for Reading duty.
        _db.Eligibilities.Add(new Eligibility
        {
            MemberId = beatrice.MemberId,
            DutyId = readingDuty.DutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = admin.AdminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        _db.Eligibilities.Add(new Eligibility
        {
            MemberId = dana.MemberId,
            DutyId = readingDuty.DutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = admin.AdminId,
            RevokedDate = null,
            RevokedReason = null,
        });
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

        // Existing assignment: Beatrice on Reading duty, NULL status,
        // created "now" (inside the timeout window).
        var assignment = new RosterAssignment
        {
            MemberId = beatrice.MemberId,
            DutyId = readingDuty.DutyId,
            OccurrenceId = occurrence.OccurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        };
        _db.RosterAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        return new Fixture(
            occurrence.OccurrenceId,
            readingDuty.DutyId,
            assignment.AssignmentId,
            beatrice.MemberId,
            dana.MemberId,
            proposed.AssignmentStatusId,
            confirmed.AssignmentStatusId,
            declined.AssignmentStatusId);
    }

    // -----------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------

    [Fact]
    public async Task Respond_WithNonTerminalStatus_TransitionsWithoutReplacement()
    {
        var fixture = await SeedFixtureAsync();
        var service = new ManageConfirmationService(_db, _notification, _clock);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            AssignmentId = fixture.AssignmentId,
            TargetStatusId = fixture.ConfirmedStatusId,
            IsSweep = false,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(0);

        var assignment = await _db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.AssignmentStatusId.Should().Be(fixture.ConfirmedStatusId);

        var totalAssignments = await _db.RosterAssignments.CountAsync();
        totalAssignments.Should().Be(1);
    }

    [Fact]
    public async Task Respond_WithTerminalStatus_AndCandidateAvailable_CreatesReplacement()
    {
        var fixture = await SeedFixtureAsync();
        var service = new ManageConfirmationService(_db, _notification, _clock);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            AssignmentId = fixture.AssignmentId,
            TargetStatusId = fixture.DeclinedStatusId,
            IsSweep = false,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(1);
        result.SlotsLeftVacant.Should().Be(0);

        var assignments = await _db.RosterAssignments
            .OrderBy(a => a.AssignmentId)
            .ToListAsync();

        assignments.Should().HaveCount(2);

        var original = assignments[0];
        original.AssignmentId.Should().Be(fixture.AssignmentId);
        original.AssignmentStatusId.Should().Be(fixture.DeclinedStatusId);

        var replacement = assignments[1];
        replacement.MemberId.Should().Be(fixture.DanaMemberId);
        replacement.DutyId.Should().Be(fixture.ReadingDutyId);
        replacement.OccurrenceId.Should().Be(fixture.OccurrenceId);
        replacement.AssignmentStatusId.Should().BeNull();
        replacement.AssignmentSource.Should().Be("Automatic");
        replacement.AssignedBy.Should().BeNull();
        replacement.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task Respond_WithTerminalStatus_AndNoCandidate_SlotLeftVacant()
    {
        var fixture = await SeedFixtureAsync();

        // Remove Dana's eligibility so no candidate remains.
        var danaEligibility = await _db.Eligibilities
            .Where(e => e.MemberId == fixture.DanaMemberId)
            .ToListAsync();
        _db.Eligibilities.RemoveRange(danaEligibility);
        await _db.SaveChangesAsync();

        var service = new ManageConfirmationService(_db, _notification, _clock);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            AssignmentId = fixture.AssignmentId,
            TargetStatusId = fixture.DeclinedStatusId,
            IsSweep = false,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(1);

        var totalAssignments = await _db.RosterAssignments.CountAsync();
        totalAssignments.Should().Be(1);
    }

    [Fact]
    public async Task Respond_WithTerminalStatus_ExcludesMemberAlreadyTerminalOnThisSlot()
    {
        var fixture = await SeedFixtureAsync();

        // Give Dana a terminal assignment on the same slot. She is now
        // cumulatively excluded, even though she is eligible.
        _db.RosterAssignments.Add(new RosterAssignment
        {
            MemberId = fixture.DanaMemberId,
            DutyId = fixture.ReadingDutyId,
            OccurrenceId = fixture.OccurrenceId,
            AssignmentStatusId = fixture.DeclinedStatusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        });
        await _db.SaveChangesAsync();

        var service = new ManageConfirmationService(_db, _notification, _clock);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            AssignmentId = fixture.AssignmentId,
            TargetStatusId = fixture.DeclinedStatusId,
            IsSweep = false,
        });

        result.Status.Should().Be("Success");
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(1);

        var totalAssignments = await _db.RosterAssignments.CountAsync();
        totalAssignments.Should().Be(2); // original + Dana's terminal
    }

    [Fact]
    public async Task Sweep_WithTimedOutAssignment_TransitionsAndReplaces()
    {
        var fixture = await SeedFixtureAsync();

        // Backdate the assignment's CreatedAt so it is beyond the
        // 48-hour timeout window.
        var assignment = await _db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.CreatedAt = _clock.GetUtcNow().AddHours(-72);
        await _db.SaveChangesAsync();

        var service = new ManageConfirmationService(_db, _notification, _clock);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            AssignmentId = null,
            TargetStatusId = fixture.DeclinedStatusId,
            IsSweep = true,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(1);

        var original = await _db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        original.AssignmentStatusId.Should().Be(fixture.DeclinedStatusId);
    }

    [Fact]
    public async Task Sweep_WithAssignmentInsideWindow_DoesNotTransition()
    {
        var fixture = await SeedFixtureAsync();
        var service = new ManageConfirmationService(_db, _notification, _clock);

        // Assignment's CreatedAt is "now", well inside the 48-hour
        // window. Nothing should be swept.
        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            AssignmentId = null,
            TargetStatusId = fixture.DeclinedStatusId,
            IsSweep = true,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(0);
        result.ReplacementsCreated.Should().Be(0);

        var assignment = await _db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.AssignmentStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Run_WithoutInitialAssignmentStatusSetting_Fails()
    {
        var fixture = await SeedFixtureAsync(includeInitialStatusSetting: false);
        var service = new ManageConfirmationService(_db, _notification, _clock);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            AssignmentId = fixture.AssignmentId,
            TargetStatusId = fixture.ConfirmedStatusId,
            IsSweep = false,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("InitialAssignmentStatusID");
    }

    [Fact]
    public async Task Run_WithInvalidCommand_Fails()
    {
        await SeedFixtureAsync();
        var service = new ManageConfirmationService(_db, _notification, _clock);

        var result = await service.RunAsync(new ManageConfirmationCommand());

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Command must be");
    }
}
