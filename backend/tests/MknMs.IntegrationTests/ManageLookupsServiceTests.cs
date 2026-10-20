using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageLookups;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageLookupsService's five soft-delete methods and
/// the corresponding block-only cascades.
/// </summary>
public class ManageLookupsServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<int> SeedAdminAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        await Db.SaveChangesAsync();

        var member = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(member);
        await Db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        // Fail-closed policy: §9.7 requires every lifecycle operation
        // to resolve an EntityDeletionPolicy. Seed the five entity
        // types ManageLookupsService handles.
        foreach (var entityType in new[]
        {
            "OutcomeState",
            "AssignmentStatus",
            "TimeOfDay",
            "ServiceType",
            "PermissionTier",
        })
        {
            Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
            {
                EntityType = entityType,
                RequiresApprovalForDelete = false,
                RequiresApprovalForDeactivate = false,
                RequiresReasonForDelete = true,
                RequiresReasonForDeactivate = false,
                IsActive = true,
                IsDeleted = false,
            });
        }
        await Db.SaveChangesAsync();

        return admin.AdminId;
    }

    private ManageLookupsService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task OutcomeState_Unreferenced_Succeeds()
    {
        var admin = await SeedAdminAsync();
        var state = new OutcomeState { Name = "Unfilled", IsDeleted = false };
        Db.OutcomeStates.Add(state);
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteOutcomeStateAsync(
            state.OutcomeStateId, admin, admin, true, "retired");

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.OutcomeStates
            .AsNoTracking().FirstAsync(o => o.OutcomeStateId == state.OutcomeStateId);
        persisted.IsDeleted.Should().BeTrue();
        persisted.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task OutcomeState_ReferencedBySetting_IsBlocked()
    {
        var admin = await SeedAdminAsync();
        var state = new OutcomeState { Name = "Unfilled", IsDeleted = false };
        Db.OutcomeStates.Add(state);
        await Db.SaveChangesAsync();

        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "OutcomeStateUnfilledID",
            Value = state.OutcomeStateId.ToString(),
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteOutcomeStateAsync(
            state.OutcomeStateId, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task OutcomeState_SettingValueCoincidence_DoesNotBlock()
    {
        // A ConfirmationTimeoutHours of the same numeric value must
        // not block the OutcomeState. Only the four setting keys that
        // name outcome states count as references.
        var admin = await SeedAdminAsync();
        var state = new OutcomeState { Name = "Unfilled", IsDeleted = false };
        Db.OutcomeStates.Add(state);
        await Db.SaveChangesAsync();

        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "ConfirmationTimeoutHours",
            Value = state.OutcomeStateId.ToString(),
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteOutcomeStateAsync(
            state.OutcomeStateId, admin, admin, true, "retired");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task AssignmentStatus_ReferencedByAssignment_IsBlocked()
    {
        var admin = await SeedAdminAsync();
        var status = new AssignmentStatus { Name = "Confirmed", IsDeleted = false };
        Db.AssignmentStatuses.Add(status);

        var serviceType = new ServiceType { Name = "Regular", IsDeleted = false };
        Db.ServiceTypes.Add(serviceType);

        var timeOfDay = new TimeOfDay { Name = "Morning", IsDeleted = false };
        Db.TimeOfDays.Add(timeOfDay);
        await Db.SaveChangesAsync();

        var branch = await Db.Branches.FirstAsync();
        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var def = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = serviceType.ServiceTypeId, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(def);
        await Db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = def.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsDeleted = false,
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        var occ = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = new DateOnly(2026, 11, 1),
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occ);
        await Db.SaveChangesAsync();

        var member = await Db.Members.FirstAsync();
        var duty = new Duty { Name = "Reading", IsDeleted = false };
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.RosterAssignments.Add(new MknMs.Domain.D7_RosterAssignment.RosterAssignment
        {
            MemberId = member.MemberId,
            DutyId = duty.DutyId,
            OccurrenceId = occ.OccurrenceId,
            AssignmentStatusId = status.AssignmentStatusId,
            AssignmentSource = "Automatic",
            CreatedAt = Now,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAssignmentStatusAsync(
            status.AssignmentStatusId, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task TimeOfDay_ReferencedBySlot_IsBlocked()
    {
        var admin = await SeedAdminAsync();
        var tod = new TimeOfDay { Name = "Evening", IsDeleted = false };
        Db.TimeOfDays.Add(tod);
        await Db.SaveChangesAsync();

        var branch = await Db.Branches.FirstAsync();
        Db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteTimeOfDayAsync(
            tod.TimeOfDayId, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task ServiceType_Unreferenced_Succeeds()
    {
        var admin = await SeedAdminAsync();
        var st = new ServiceType { Name = "Special", IsDeleted = false };
        Db.ServiceTypes.Add(st);
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteServiceTypeAsync(
            st.ServiceTypeId, admin, admin, true, "retired");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PermissionTier_ReferencedByAdmin_IsBlocked()
    {
        var admin = await SeedAdminAsync();
        var tier = await Db.PermissionTiers.FirstAsync(t => t.IsDefault);
        var service = MakeService();

        var result = await service.SoftDeletePermissionTierAsync(
            tier.PermissionTierId, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task AnyLookup_WithoutAcknowledgement_Fails()
    {
        var admin = await SeedAdminAsync();
        var state = new OutcomeState { Name = "Unfilled", IsDeleted = false };
        Db.OutcomeStates.Add(state);
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteOutcomeStateAsync(
            state.OutcomeStateId, admin, admin,
            consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }
    [Fact]
    public async Task PermissionTier_DefaultWithoutReferences_IsBlocked()
    {
        // The seeded default tier is referenced by the seeded admin,
        // so it would be blocked by the reference check regardless of
        // its default status. To exercise the default arm in isolation,
        // move the default flag to a new unreferenced tier first.
        var admin = await SeedAdminAsync();

        var currentDefault = await Db.PermissionTiers.FirstAsync(t => t.IsDefault);
        currentDefault.IsDefault = false;

        var newDefault = new PermissionTier { Name = "NewDefault", IsDefault = true };
        Db.PermissionTiers.Add(newDefault);
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeletePermissionTierAsync(
            newDefault.PermissionTierId, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
        result.Error.Message.Should().Contain("default");
    }
}
