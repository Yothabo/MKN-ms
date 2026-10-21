using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureServiceDutyAndSchedule;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureServiceDutyAndScheduleService.CreateScheduleAsync:
/// active-schedule uniqueness on create, and the OwningBranch invariant
/// at creation time.
/// </summary>
public class ConfigureServiceDutyAndScheduleServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 20, 12, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private sealed record Fixture(
        int ScheduleId, int AdminId, int ServiceDefId, int SlotId,
        int BranchId, int OtherBranchId);

    private async Task<Fixture> SeedAsync(bool withOwningBranch = false)
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var otherBranch = new Branch { Name = "B2", Location = "L2", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var st = new ServiceType { Name = "Regular" };
        var tod = new TimeOfDay { Name = "Morning" };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.Branches.Add(otherBranch);
        Db.PermissionTiers.Add(tier);
        Db.ServiceTypes.Add(st);
        Db.TimeOfDays.Add(tod);
        await Db.SaveChangesAsync();

        var adminMember = new Member
        {
            Name = "Admin", Surname = "User",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(adminMember);
        await Db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = adminMember.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsActive = true, IsDeleted = false,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var def = new ServiceDefinition
        {
            Name = "Sunday",
            ServiceTypeId = st.ServiceTypeId,
            OwningBranchId = withOwningBranch ? branch.BranchId : null,
            IsActive = true, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(def);
        await Db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = def.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsActive = true, IsDeleted = false,
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        return new Fixture(
            schedule.ScheduleId, admin.AdminId, def.ServiceDefId,
            slot.TimeSlotId, branch.BranchId, otherBranch.BranchId);
    }

    private ConfigureServiceDutyAndScheduleService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    // Matrix row 1.0:2a785f99643b: A ServiceSchedule with `IsActive = false` | The new schedule is created
    [Fact]
    [Trait("MatrixRow", "1.0:2a785f99643b")]
    public async Task CreateSchedule_WithInactivePriorSchedule_Succeeds()
    {
        var f = await SeedAsync();
        var service = MakeService();

        // Deactivate the existing schedule, then create a new one for the
        // same (ServiceDefId, TimeSlotId) pair.
        var prior = await Db.ServiceSchedules
            .FirstAsync(s => s.ScheduleId == f.ScheduleId);
        prior.IsActive = false;
        await Db.SaveChangesAsync();

        var result = await service.CreateScheduleAsync(
            f.ServiceDefId, f.SlotId, new TimeOnly(10, 0), f.AdminId);

        result.IsSuccess.Should().BeTrue(
            $"uniqueness applies only to active rows; error: {result.Error?.Message}");

        var activeCount = await Db.ServiceSchedules
            .CountAsync(s => s.ServiceDefId == f.ServiceDefId
                && s.TimeSlotId == f.SlotId
                && s.IsActive);
        activeCount.Should().Be(1);
    }

    // Matrix row 1.0:313812a49b14: An active ServiceSchedule and a second active schedule for the same (ServiceDefID, TimeSlotID) | The operation is rejected with `Conflict`
    [Fact]
    [Trait("MatrixRow", "1.0:313812a49b14")]
    public async Task CreateSchedule_SecondActiveForSamePair_Conflict()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateScheduleAsync(
            f.ServiceDefId, f.SlotId, new TimeOnly(10, 0), f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.DuplicateName);

        var activeCount = await Db.ServiceSchedules
            .CountAsync(s => s.ServiceDefId == f.ServiceDefId
                && s.TimeSlotId == f.SlotId
                && s.IsActive);
        activeCount.Should().Be(1);
    }

    // Matrix row 1.0:93664c041f5a: A ServiceDefinition with a non-null OwningBranchID | The operation is rejected with `ValidationFailed`, per B19
    [Fact]
    [Trait("MatrixRow", "1.0:93664c041f5a")]
    public async Task CreateSchedule_OwningBranchMismatch_ValidationFailed()
    {
        var f = await SeedAsync(withOwningBranch: true);
        var service = MakeService();

        // Add a second slot at a different branch. Its BranchId != the
        // definition's OwningBranchId, so create-schedule must reject.
        var tod = await Db.TimeOfDays.FirstAsync();
        var otherSlot = new BranchTimeSlot
        {
            BranchId = f.OtherBranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsActive = true, IsDeleted = false,
        };
        Db.BranchTimeSlots.Add(otherSlot);
        await Db.SaveChangesAsync();

        var result = await service.CreateScheduleAsync(
            f.ServiceDefId, otherSlot.TimeSlotId, new TimeOnly(10, 0), f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);

        var exists = await Db.ServiceSchedules
            .AnyAsync(s => s.ServiceDefId == f.ServiceDefId
                && s.TimeSlotId == otherSlot.TimeSlotId);
        exists.Should().BeFalse("the mismatched schedule was not created");
    }
}
