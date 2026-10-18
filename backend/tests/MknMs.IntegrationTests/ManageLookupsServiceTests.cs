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
public class ManageLookupsServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 10, 0, 0, TimeSpan.Zero);

    private MknDbContext _db = null!;
    private TimeProvider _clock = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);
        _clock = new FakeTimeProvider(Now);

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE configuration_audit_log, entity_deletion_policy, " +
            "notification_subscription, service_occurrence, service_schedule, " +
            "service_definition, service_type, branch_time_slot, time_of_day, " +
            "roster_assignment, assignment_status, outcome_state, " +
            "member, member_status, branch, role, duty, admin, permission_tier, " +
            "system_setting " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> SeedAdminAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var status = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        await _db.SaveChangesAsync();

        var member = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId,
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
            _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
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
        await _db.SaveChangesAsync();

        return admin.AdminId;
    }

    private ManageLookupsService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task OutcomeState_Unreferenced_Succeeds()
    {
        var admin = await SeedAdminAsync();
        var state = new OutcomeState { Name = "Unfilled", IsDeleted = false };
        _db.OutcomeStates.Add(state);
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteOutcomeStateAsync(
            state.OutcomeStateId, admin, admin, true, "retired");

        result.IsSuccess.Should().BeTrue();

        var persisted = await _db.OutcomeStates
            .AsNoTracking().FirstAsync(o => o.OutcomeStateId == state.OutcomeStateId);
        persisted.IsDeleted.Should().BeTrue();
        persisted.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task OutcomeState_ReferencedBySetting_IsBlocked()
    {
        var admin = await SeedAdminAsync();
        var state = new OutcomeState { Name = "Unfilled", IsDeleted = false };
        _db.OutcomeStates.Add(state);
        await _db.SaveChangesAsync();

        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "OutcomeStateUnfilledID",
            Value = state.OutcomeStateId.ToString(),
        });
        await _db.SaveChangesAsync();

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
        _db.OutcomeStates.Add(state);
        await _db.SaveChangesAsync();

        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "ConfirmationTimeoutHours",
            Value = state.OutcomeStateId.ToString(),
        });
        await _db.SaveChangesAsync();

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
        _db.AssignmentStatuses.Add(status);

        var serviceType = new ServiceType { Name = "Regular", IsDeleted = false };
        _db.ServiceTypes.Add(serviceType);

        var timeOfDay = new TimeOfDay { Name = "Morning", IsDeleted = false };
        _db.TimeOfDays.Add(timeOfDay);
        await _db.SaveChangesAsync();

        var branch = await _db.Branches.FirstAsync();
        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var def = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = serviceType.ServiceTypeId, IsDeleted = false,
        };
        _db.ServiceDefinitions.Add(def);
        await _db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = def.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsDeleted = false,
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

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
        _db.ServiceOccurrences.Add(occ);
        await _db.SaveChangesAsync();

        var member = await _db.Members.FirstAsync();
        var duty = new Duty { Name = "Reading", IsDeleted = false };
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

        _db.RosterAssignments.Add(new MknMs.Domain.D7_RosterAssignment.RosterAssignment
        {
            MemberId = member.MemberId,
            DutyId = duty.DutyId,
            OccurrenceId = occ.OccurrenceId,
            AssignmentStatusId = status.AssignmentStatusId,
            AssignmentSource = "Automatic",
            CreatedAt = Now,
        });
        await _db.SaveChangesAsync();

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
        _db.TimeOfDays.Add(tod);
        await _db.SaveChangesAsync();

        var branch = await _db.Branches.FirstAsync();
        _db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

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
        _db.ServiceTypes.Add(st);
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteServiceTypeAsync(
            st.ServiceTypeId, admin, admin, true, "retired");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task PermissionTier_ReferencedByAdmin_IsBlocked()
    {
        var admin = await SeedAdminAsync();
        var tier = await _db.PermissionTiers.FirstAsync(t => t.IsDefault);
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
        _db.OutcomeStates.Add(state);
        await _db.SaveChangesAsync();

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

        var currentDefault = await _db.PermissionTiers.FirstAsync(t => t.IsDefault);
        currentDefault.IsDefault = false;

        var newDefault = new PermissionTier { Name = "NewDefault", IsDefault = true };
        _db.PermissionTiers.Add(newDefault);
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeletePermissionTierAsync(
            newDefault.PermissionTierId, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
        result.Error.Message.Should().Contain("default");
    }
}
