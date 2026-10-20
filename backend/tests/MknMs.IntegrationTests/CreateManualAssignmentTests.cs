using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using MknMs.Application.Common;
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
public class CreateManualAssignmentTests : IntegrationTestBase
{
    private readonly IDispatchNotificationService _notification = new FakeDispatchNotificationService();
    private readonly TimeProvider _clock = new FakeTimeProvider(
        new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));

    protected override async Task OnInitializedAsync()
    {
        await TestSeed.EnsureRequiredSettingsAsync(Db);
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

        Db.ServiceTypes.Add(serviceType);
        Db.TimeOfDays.Add(timeOfDay);
        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.Duties.Add(duty);
        Db.PermissionTiers.Add(tier);
        Db.AssignmentStatuses.Add(status);
        await Db.SaveChangesAsync();

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var eligible = new Member
        {
            Name = "Dana", Surname = "Prototype",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        var ineligible = new Member
        {
            Name = "Beatrice", Surname = "Sample",
            JoinDate = new DateOnly(2024, 6, 1),
            DateOfBirth = new DateOnly(1995, 3, 12),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0001", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(eligible);
        Db.Members.Add(ineligible);
        await Db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = eligible.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty.DutyId,
            RequiredSlotCount = 1,
        });
        await Db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        if (withEligibilityFlagRule)
        {
            // Eligibility Flag criterion referencing this same duty.
            Db.DutyRules.Add(new DutyRule
            {
                DutyId = duty.DutyId,
                TierOrder = 1,
                CriteriaType = "EligibilityFlag",
                CriteriaValue = duty.DutyId.ToString(),
            });
            await Db.SaveChangesAsync();

            // Only the "eligible" member holds the grant.
            Db.Eligibilities.Add(new Eligibility
            {
                MemberId = eligible.MemberId,
                DutyId = duty.DutyId,
                GrantedDate = new DateOnly(2025, 1, 1),
                GrantedBy = admin.AdminId,
                RevokedDate = null,
                RevokedReason = null,
            });
            await Db.SaveChangesAsync();
        }
        else
        {
            // Role-based rule the "ineligible" member does not match,
            // to prove manual assignment bypasses non-EligibilityFlag
            // criteria. Rule requires a role neither member holds.
            var unusedRole = new Role { Name = "Steward", IsActive = true };
            Db.Roles.Add(unusedRole);
            await Db.SaveChangesAsync();

            Db.DutyRules.Add(new DutyRule
            {
                DutyId = duty.DutyId,
                TierOrder = 1,
                CriteriaType = "Role",
                CriteriaValue = "Steward",
            });
            await Db.SaveChangesAsync();
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
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

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
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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

        var row = await Db.RosterAssignments
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
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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

        var row = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == result.AssignmentId);
        row.AssignmentStatusId.Should().Be(f.NonTerminalStatusId);
    }

    [Fact]
    public async Task Create_Duplicate_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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

        var count = await Db.RosterAssignments.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Create_WithEligibilityFlagRule_AndGrant_Succeeds()
    {
        var f = await SeedFixtureAsync(withEligibilityFlagRule: true);
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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
    public async Task Create_WithMissingDutyOccurrenceAndTargetStatus_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

        // Missing duty; all other referents valid.
        var missingDuty = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = 9999,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });
        missingDuty.Status.Should().Be("Failure");
        missingDuty.ErrorDetail.Should().Contain("Duty 9999");

        // Missing occurrence; all other referents valid.
        var missingOccurrence = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = 9999,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });
        missingOccurrence.Status.Should().Be("Failure");
        missingOccurrence.ErrorDetail.Should().Contain("ServiceOccurrence 9999");

        // Missing target status; all other referents valid.
        var missingStatus = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = 9999,
            ActingAdminId = f.AdminId,
        });
        missingStatus.Status.Should().Be("Failure");
        missingStatus.ErrorDetail.Should().Contain("AssignmentStatus 9999");
    }

    [Fact]
    public async Task Create_WithNonTerminalAssignmentOnOccurrence_Succeeds()
    {
        // §17.1 B4: manual assignment does not enforce the
        // one-member-per-occurrence availability rule. A member who
        // already holds a non-terminal assignment on the occurrence
        // may be assigned to a second duty.
        var f = await SeedFixtureAsync();

        var secondDuty = new Duty { Name = "Welcome duty", IsActive = true };
        Db.Duties.Add(secondDuty);
        await Db.SaveChangesAsync();

        var proposal = new AssignmentStatus { Name = "Proposed", IsTerminal = false };
        Db.AssignmentStatuses.Add(proposal);
        await Db.SaveChangesAsync();

        // Seed a non-terminal assignment for the eligible member on
        // the same occurrence, on a different duty.
        Db.RosterAssignments.Add(new RosterAssignment
        {
            MemberId = f.EligibleMemberId,
            DutyId = secondDuty.DutyId,
            OccurrenceId = f.OccurrenceId,
            AssignmentStatusId = proposal.AssignmentStatusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        });
        await Db.SaveChangesAsync();

        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = f.NonTerminalStatusId,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Success");

        var total = await Db.RosterAssignments.CountAsync();
        total.Should().Be(2);
    }

    [Fact]
    public async Task Create_DoesNotEnforceSlotCapacity()
    {
        var f = await SeedFixtureAsync();
        var service = new CreateManualAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db));

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
        var total = await Db.RosterAssignments.CountAsync();
        total.Should().Be(2);
    }

    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        // C6 / §13.6: the acting admin must resolve to an Admin row
        // whose IsActive = true. An inactive admin is rejected.
        var f = await SeedFixtureAsync();

        var admin = await Db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new CreateManualAssignmentService(
            Db, _notification, _clock, new OccurrenceLockService(Db));

        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = f.EligibleMemberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            TargetStatusId = null,
            ActingAdminId = f.AdminId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("is not active");

        var count = await Db.RosterAssignments.CountAsync();
        count.Should().Be(0);
    }
}
