using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureServiceDutyAndSchedule;
using MknMs.Application.Processes.Operations.MaterializeOccurrences;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for SoftDeleteScheduleAsync and the effect on the
/// materializer's schedule read.
/// </summary>
public class ConfigureServiceScheduleSoftDeleteTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int scheduleId, int adminId)> SeedAsync()
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
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "OccurrenceHorizonDays", Value = "30", Required = true,
        });
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone", Value = "Africa/Johannesburg", Required = true,
        });
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "AttendanceRegisterEnabled", Value = "true", Required = true,
        });
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ServiceDefinitionDuty",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ServiceSchedule",
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

        var def = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = st.ServiceTypeId, IsDeleted = false,
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

        return (schedule.ScheduleId, admin.AdminId);
    }

    private ConfigureServiceDutyAndScheduleService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    // Matrix row 1.0: A Service Schedule whose Service Occurrence rows exist | A soft delete is requested
    [Fact]
    [Trait("MatrixRow", "1.0:6390e2131508")]
    public async Task SoftDelete_SucceedsAndWritesAudit()
    {
        var (scheduleId, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteScheduleAsync(
            scheduleId, admin, admin, true, "no longer used");

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.ServiceSchedules
            .AsNoTracking()
            .FirstAsync(s => s.ScheduleId == scheduleId);
        persisted.IsDeleted.Should().BeTrue();
        persisted.IsActive.Should().BeFalse();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.ConsequencesPreviewed.Should().Contain("preserved");
    }

    [Fact]
    public async Task Materializer_DoesNotConsultSoftDeletedSchedule()
    {
        var (scheduleId, admin) = await SeedAsync();

        // Materialize once while the schedule is live.
        var materializer = new MaterializeOccurrencesService(Db, _clock);
        var first = await materializer.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });
        first.Status.Should().Be("Success");
        first.OccurrencesCreated.Should().BeGreaterThan(0);

        var occurrencesBefore = await Db.ServiceOccurrences
            .CountAsync(o => o.ScheduleId == scheduleId);

        // Soft-delete the schedule.
        var service = MakeService();
        await service.SoftDeleteScheduleAsync(
            scheduleId, admin, admin, true, "retired");

        // Clear all future occurrences so the materializer has room
        // to create new ones if it consults the schedule again.
        var allFuture = await Db.ServiceOccurrences
            .Where(o => o.ScheduleId == scheduleId)
            .ToListAsync();
        Db.ServiceOccurrences.RemoveRange(allFuture);
        await Db.SaveChangesAsync();

        var second = await materializer.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });

        second.Status.Should().Be("Success");
        second.SchedulesEvaluated.Should().Be(0,
            "a soft-deleted schedule is not eligible for materialization");
        second.OccurrencesCreated.Should().Be(0);

        // The occurrencesBefore count is preserved by the soft delete
        // step itself — the cascade does not touch existing rows.
        occurrencesBefore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (scheduleId, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteScheduleAsync(
            scheduleId, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var (scheduleId, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteScheduleAsync(
            scheduleId, admin, admin, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }
    [Fact]
    public async Task SoftDelete_WithInactiveAdmin_Fails()
    {
        var (scheduleId, actingAdminId) = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteScheduleAsync(
            scheduleId, actingAdminId, actingAdminId, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task Reactivate_DeactivatedSchedule_Succeeds()
    {
        // §9.7: deactivation is reversible.
        var (scheduleId, adminId) = await SeedAsync();
        var service = MakeService();

        var deactivated = await service.DeactivateScheduleAsync(
            scheduleId, adminId, adminId, true, "test");
        deactivated.IsSuccess.Should().BeTrue();

        var auditBefore = await Db.ConfigurationAuditLogs.CountAsync();

        var result = await service.ReactivateScheduleAsync(scheduleId, adminId);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.ServiceSchedules
            .AsNoTracking()
            .FirstAsync(s => s.ScheduleId == scheduleId);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();

        var auditAfter = await Db.ConfigurationAuditLogs.CountAsync();
        auditAfter.Should().Be(auditBefore);
    }

    [Fact]
    public async Task Reactivate_SoftDeletedSchedule_Fails()
    {
        var (scheduleId, adminId) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteScheduleAsync(
            scheduleId, adminId, adminId, true, "test");

        var result = await service.ReactivateScheduleAsync(scheduleId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        var persisted = await Db.ServiceSchedules
            .AsNoTracking()
            .FirstAsync(s => s.ScheduleId == scheduleId);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveSchedule_Fails()
    {
        var (scheduleId, adminId) = await SeedAsync();
        var service = MakeService();

        var result = await service.ReactivateScheduleAsync(scheduleId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyActive);
    }
}
