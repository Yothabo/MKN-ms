using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureTimeSlot;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureTimeSlotService.SoftDeleteAsync and the
/// BranchTimeSlot cascade.
/// </summary>
public class ConfigureTimeSlotServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 18, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int slotId, int adminId, int defId)> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var tod = new TimeOfDay { Name = "Morning" };
        var st = new ServiceType { Name = "Regular" };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.TimeOfDays.Add(tod);
        Db.ServiceTypes.Add(st);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "BranchTimeSlot",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
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

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsDeleted = false,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = st.ServiceTypeId, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        return (slot.TimeSlotId, admin.AdminId, definition.ServiceDefId);
    }

    private ConfigureTimeSlotService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_WithActiveSchedule_IsBlocked()
    {
        var (slot, admin, def) = await SeedAsync();

        Db.ServiceSchedules.Add(new ServiceSchedule
        {
            ServiceDefId = def,
            TimeSlotId = slot,
            StartTime = new TimeOnly(9, 0),
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            slot, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        Db.ChangeTracker.Clear();
        var persisted = await Db.BranchTimeSlots
            .AsNoTracking()
            .FirstAsync(t => t.TimeSlotId == slot);
        persisted.IsDeleted.Should().BeFalse();

        var schedules = await Db.ServiceSchedules.CountAsync();
        schedules.Should().Be(1);

        var audit = await Db.ConfigurationAuditLogs.CountAsync();
        audit.Should().Be(0);
    }

    [Fact]
    public async Task SoftDelete_WithInactiveSchedule_RemovesIt()
    {
        var (slot, admin, def) = await SeedAsync();

        Db.ServiceSchedules.Add(new ServiceSchedule
        {
            ServiceDefId = def,
            TimeSlotId = slot,
            StartTime = new TimeOnly(9, 0),
            IsActive = false,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            slot, admin, admin, true, "not used");

        result.IsSuccess.Should().BeTrue();

        var schedules = await Db.ServiceSchedules.CountAsync();
        schedules.Should().Be(0);

        var persisted = await Db.BranchTimeSlots
            .AsNoTracking()
            .FirstAsync(t => t.TimeSlotId == slot);
        persisted.IsDeleted.Should().BeTrue();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_NoSchedules_Succeeds()
    {
        var (slot, admin, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            slot, admin, admin, true, "not used");

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (slot, admin, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            slot, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var (slot, admin, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            slot, admin, admin, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }
    [Fact]
    public async Task Deactivate_WithInactiveAdmin_Fails()
    {
        var (slotId, actingAdminId, _) = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.DeactivateAsync(
            slotId, actingAdminId, actingAdminId, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task Reactivate_DeactivatedTimeSlot_Succeeds()
    {
        // §9.7: deactivation is reversible.
        var (slotId, adminId, _) = await SeedAsync();
        var service = MakeService();

        var deactivated = await service.DeactivateAsync(
            slotId, adminId, adminId, true, "test");
        deactivated.IsSuccess.Should().BeTrue();

        var auditBefore = await Db.ConfigurationAuditLogs.CountAsync();

        var result = await service.ReactivateAsync(slotId, adminId);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.BranchTimeSlots
            .AsNoTracking()
            .FirstAsync(t => t.TimeSlotId == slotId);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();

        // §9.7's audit scope is deactivation and soft delete; reactivation
        // writes no row.
        var auditAfter = await Db.ConfigurationAuditLogs.CountAsync();
        auditAfter.Should().Be(auditBefore);
    }

    [Fact]
    public async Task Reactivate_SoftDeletedTimeSlot_Fails()
    {
        var (slotId, adminId, _) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(slotId, adminId, adminId, true, "test");

        var result = await service.ReactivateAsync(slotId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        var persisted = await Db.BranchTimeSlots
            .AsNoTracking()
            .FirstAsync(t => t.TimeSlotId == slotId);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveTimeSlot_Fails()
    {
        var (slotId, adminId, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.ReactivateAsync(slotId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyActive);
    }
}
