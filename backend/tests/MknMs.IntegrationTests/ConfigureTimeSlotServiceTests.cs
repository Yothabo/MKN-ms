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

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureTimeSlotService.SoftDeleteAsync and the
/// BranchTimeSlot cascade.
/// </summary>
public class ConfigureTimeSlotServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 18, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, service_schedule, service_definition, " +
            "service_type, branch_time_slot, time_of_day, member, member_status, " +
            "branch, role, admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(int slotId, int adminId, int defId)> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsActive = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsActive = true, IsDeleted = false };
        var status = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true,
            IsActive = true, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true,
            IsActive = true, IsDeleted = false,
        };
        var tod = new TimeOfDay { Name = "Morning" };
        var st = new ServiceType { Name = "Regular" };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.TimeOfDays.Add(tod);
        _db.ServiceTypes.Add(st);
        await _db.SaveChangesAsync();

        var adminMember = new Member
        {
            Name = "Admin", Surname = "User",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId, IsActive = true,
        };
        _db.Members.Add(adminMember);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = adminMember.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        _db.Admins.Add(admin);

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsActive = true,
            IsDeleted = false,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = st.ServiceTypeId,
            IsActive = true, IsDeleted = false,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        return (slot.TimeSlotId, admin.AdminId, definition.ServiceDefId);
    }

    private ConfigureTimeSlotService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_WithActiveSchedule_IsBlocked()
    {
        var (slot, admin, def) = await SeedAsync();

        _db.ServiceSchedules.Add(new ServiceSchedule
        {
            ServiceDefId = def,
            TimeSlotId = slot,
            StartTime = new TimeOnly(9, 0),
            IsActive = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            slot, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        _db.ChangeTracker.Clear();
        var persisted = await _db.BranchTimeSlots
            .AsNoTracking()
            .FirstAsync(t => t.TimeSlotId == slot);
        persisted.IsDeleted.Should().BeFalse();

        var schedules = await _db.ServiceSchedules.CountAsync();
        schedules.Should().Be(1);

        var audit = await _db.ConfigurationAuditLogs.CountAsync();
        audit.Should().Be(0);
    }

    [Fact]
    public async Task SoftDelete_WithInactiveSchedule_RemovesIt()
    {
        var (slot, admin, def) = await SeedAsync();

        _db.ServiceSchedules.Add(new ServiceSchedule
        {
            ServiceDefId = def,
            TimeSlotId = slot,
            StartTime = new TimeOnly(9, 0),
            IsActive = false,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            slot, admin, admin, true, "not used");

        result.IsSuccess.Should().BeTrue();

        var schedules = await _db.ServiceSchedules.CountAsync();
        schedules.Should().Be(0);

        var persisted = await _db.BranchTimeSlots
            .AsNoTracking()
            .FirstAsync(t => t.TimeSlotId == slot);
        persisted.IsDeleted.Should().BeTrue();

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
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
}
