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
public class ConfigureServiceScheduleSoftDeleteTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

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
            "TRUNCATE TABLE materializer_run, service_occurrence, " +
            "configuration_audit_log, entity_deletion_policy, " +
            "notification_subscription, service_schedule, service_definition_duty, " +
            "service_definition, service_type, branch_time_slot, time_of_day, " +
            "member, member_status, branch, role, duty, admin, permission_tier, " +
            "system_setting " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(int scheduleId, int adminId)> SeedAsync()
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
        var tod = new TimeOfDay { Name = "Morning" };
        var st = new ServiceType { Name = "Regular" };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.TimeOfDays.Add(tod);
        _db.ServiceTypes.Add(st);
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "OccurrenceHorizonDays", Value = "30", Required = true,
        });
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "ApplicationTimeZone", Value = "Africa/Johannesburg", Required = true,
        });
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "AttendanceRegisterEnabled", Value = "true", Required = true,
        });
        await _db.SaveChangesAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ServiceDefinitionDuty",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ServiceSchedule",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();


        var adminMember = new Member
        {
            Name = "Admin", Surname = "User",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId,
        };
        _db.Members.Add(adminMember);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = adminMember.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsDeleted = false,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var def = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = st.ServiceTypeId, IsDeleted = false,
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

        return (schedule.ScheduleId, admin.AdminId);
    }

    private ConfigureServiceDutyAndScheduleService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_SucceedsAndWritesAudit()
    {
        var (scheduleId, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteScheduleAsync(
            scheduleId, admin, admin, true, "no longer used");

        result.IsSuccess.Should().BeTrue();

        var persisted = await _db.ServiceSchedules
            .AsNoTracking()
            .FirstAsync(s => s.ScheduleId == scheduleId);
        persisted.IsDeleted.Should().BeTrue();
        persisted.IsActive.Should().BeFalse();

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.ConsequencesPreviewed.Should().Contain("preserved");
    }

    [Fact]
    public async Task Materializer_DoesNotConsultSoftDeletedSchedule()
    {
        var (scheduleId, admin) = await SeedAsync();

        // Materialize once while the schedule is live.
        var materializer = new MaterializeOccurrencesService(_db, _clock);
        var first = await materializer.RunAsync(new MaterializeOccurrencesCommand
        {
            TriggerType = "Scheduled",
        });
        first.Status.Should().Be("Success");
        first.OccurrencesCreated.Should().BeGreaterThan(0);

        var occurrencesBefore = await _db.ServiceOccurrences
            .CountAsync(o => o.ScheduleId == scheduleId);

        // Soft-delete the schedule.
        var service = MakeService();
        await service.SoftDeleteScheduleAsync(
            scheduleId, admin, admin, true, "retired");

        // Clear all future occurrences so the materializer has room
        // to create new ones if it consults the schedule again.
        var allFuture = await _db.ServiceOccurrences
            .Where(o => o.ScheduleId == scheduleId)
            .ToListAsync();
        _db.ServiceOccurrences.RemoveRange(allFuture);
        await _db.SaveChangesAsync();

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
}
