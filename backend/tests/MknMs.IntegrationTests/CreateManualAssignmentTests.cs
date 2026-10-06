using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MknMs.Application.Processes.Operations.CreateManualAssignment;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D10_ConfigLookups;
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
/// Integration tests for process 12.0 Create Manual Assignment.
/// </summary>
public class CreateManualAssignmentTests : IAsyncLifetime
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

    private sealed record Fixture(
        int OccurrenceId,
        int DutyId,
        int EligibleMemberId,
        int IneligibleMemberId,
        int AdminId,
        int NonTerminalStatusId);

    private async Task<Fixture> SeedFixtureAsync(bool withEligibilityFlagRule = false)
    {
        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var role = new Role { Name = "Coordinator", IsActive = true };
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        var duty = new Duty { Name = "Reading duty", IsActive = true };
        var tier = new PermissionTier { Name = "Full Admin" };
        var status = new AssignmentStatus { Name = "Confirmed", IsTerminal = false };

        _db.ServiceTypes.Add(serviceType);
        _db.TimeOfDays.Add(timeOfDay);
        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.Duties.Add(duty);
        _db.PermissionTiers.Add(tier);
        _db.AssignmentStatuses.Add(status);
        await _db.SaveChangesAsync();

        var eligible = new Member
        {
            Name = "Dana", Surname = "Prototype",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId, IsActive = true,
        };
        var ineligible = new Member
        {
            Name = "Beatrice", Surname = "Sample",
            JoinDate = new DateOnly(2024, 6, 1),
            DateOfBirth = new DateOnly(1995, 3, 12),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0001", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId, IsActive = true,
        };
        _db.Members.Add(eligible);
        _db.Members.Add(ineligible);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = eligible.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

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

        _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty.DutyId,
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

        if (withEligibilityFlagRule)
        {
            // Eligibility Flag criterion referencing this same duty.
            _db.DutyRules.Add(new DutyRule
            {
                DutyId = duty.DutyId,
                TierOrder = 1,
                CriteriaType = "EligibilityFlag",
                CriteriaValue = duty.DutyId.ToString(),
                IsActive = true,
            });
            await _db.SaveChangesAsync();

            // Only the "eligible" member holds the grant.
            _db.Eligibilities.Add(new Eligibility
            {
                MemberId = eligible.MemberId,
                DutyId = duty.DutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = admin.AdminId,
                RevokedDate = null,
                RevokedReason = null,
            });
            await _db.SaveChangesAsync();
        }
        else
        {
            // Role-based rule the "ineligible" member does not match,
            // to prove manual assignment bypasses non-EligibilityFlag
            // criteria. Rule requires a role neither member holds.
            var unusedRole = new Role { Name = "Steward", IsActive = true };
            _db.Roles.Add(unusedRole);
            await _db.SaveChangesAsync();

            _db.DutyRules.Add(new DutyRule
            {
                DutyId = duty.DutyId,
                TierOrder = 1,
                CriteriaType = "Role",
                CriteriaValue = "Steward",
                IsActive = true,
            });
            await _db.SaveChangesAsync();
        }

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
            duty.DutyId,
            eligible.MemberId,
            ineligible.MemberId,
            admin.AdminId,
            status.AssignmentStatusId);
    }

    [Fact]
    public async Task Create_WithNullStatus_WritesRowAndDispatchesNotification()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
        result.NotificationDispatched.Should().BeTrue();
        result.AssignmentId.Should().BeGreaterThan(0);

        var row = await _db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == result.AssignmentId);

        row.AssignmentSource.Should().Be("Manual");
        row.AssignedBy.Should().Be(f.AdminId);
        row.AssignmentStatusId.Should().BeNull();
        row.ApprovedBy.Should().BeNull();
    }

    [Fact]
    public async Task Create_WithNonNullStatus_DoesNotDispatchNotification()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = f.NonTerminalStatusId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
        result.NotificationDispatched.Should().BeFalse();

        var row = await _db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == result.AssignmentId);
        row.AssignmentStatusId.Should().Be(f.NonTerminalStatusId);
    }

    [Fact]
    public async Task Create_Duplicate_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });

        var second = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });

        second.Status.Should().Be("Failure");
        second.ErrorDetail.Should().Contain("already exists");
    }

    [Fact]
    public async Task Create_WithEligibilityFlagRule_AndNoGrant_Fails()
    {
        var f = await SeedFixtureAsync(withEligibilityFlagRule: true);
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        // The "ineligible" member has no grant.
        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.IneligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Eligibility");

        var count = await _db.RosterAssignments.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithEligibilityFlagRule_AndGrant_Succeeds()
    {
        var f = await SeedFixtureAsync(withEligibilityFlagRule: true);
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
    }

    [Fact]
    public async Task Create_WithNonEligibilityCriteria_BypassesThem()
    {
        var f = await SeedFixtureAsync(withEligibilityFlagRule: false);
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        // The duty has a Role=Steward rule. Neither member holds
        // Steward. Manual assignment bypasses it. §17.1 B1.
        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.IneligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");
    }

    [Fact]
    public async Task Create_WithMissingReferents_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        var missingMember = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = 9999,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });
        missingMember.Status.Should().Be("Failure");
        missingMember.ErrorDetail.Should().Contain("Member 9999");

        var missingAdmin = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = 9999,
        });
        missingAdmin.Status.Should().Be("Failure");
        missingAdmin.ErrorDetail.Should().Contain("Admin 9999");
    }

    [Fact]
    public async Task Create_DoesNotEnforceSlotCapacity()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(_db, _notification, _clock);

        // Fill the slot via 12.0 (first assignment).
        var first = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = f.NonTerminalStatusId,
            ActingAdminId = f.AdminId,
        });
        first.Status.Should().Be("Success");

        // Add a second manual assignment for the same slot with a
        // different member. Slot capacity is not enforced. §17.1 B3.
        var second = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.IneligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = f.NonTerminalStatusId,
            ActingAdminId = f.AdminId,
        });

        second.Status.Should().Be("Success");
        var total = await _db.RosterAssignments.CountAsync();
        total.Should().Be(2);
    }
}
