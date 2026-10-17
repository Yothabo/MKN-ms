using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureServiceDefinition;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureServiceDefinitionService.SoftDeleteAsync.
/// </summary>
public class ConfigureServiceDefinitionServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 20, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, service_schedule, duty_rule, " +
            "service_definition, service_type, branch_time_slot, time_of_day, " +
            "member, member_status, branch, role, duty, admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(int defId, int adminId, int stId)> SeedAsync()
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
        var st = new ServiceType { Name = "Regular" };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.ServiceTypes.Add(st);
        await _db.SaveChangesAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ServiceDefinition",
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

        var def = new ServiceDefinition
        {
            Name = "Sunday", ServiceTypeId = st.ServiceTypeId, IsDeleted = false,
        };
        _db.ServiceDefinitions.Add(def);
        await _db.SaveChangesAsync();

        return (def.ServiceDefId, admin.AdminId, st.ServiceTypeId);
    }

    private ConfigureServiceDefinitionService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_Unreferenced_Succeeds()
    {
        var (def, admin, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            def, admin, admin, true, "unused");

        result.IsSuccess.Should().BeTrue();

        var persisted = await _db.ServiceDefinitions
            .AsNoTracking()
            .FirstAsync(s => s.ServiceDefId == def);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDelete_ReferencedBySchedule_IsBlocked()
    {
        var (def, admin, _) = await SeedAsync();

        var tod = new TimeOfDay { Name = "Morning" };
        _db.TimeOfDays.Add(tod);
        await _db.SaveChangesAsync();

        var branch = await _db.Branches.FirstAsync();
        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = tod.TimeOfDayId,
            IsDeleted = false,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        _db.ServiceSchedules.Add(new ServiceSchedule
        {
            ServiceDefId = def,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            def, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        _db.ChangeTracker.Clear();
        var persisted = await _db.ServiceDefinitions
            .AsNoTracking()
            .FirstAsync(s => s.ServiceDefId == def);
        persisted.IsDeleted.Should().BeFalse();

        var audit = await _db.ConfigurationAuditLogs.CountAsync();
        audit.Should().Be(0);
    }

    [Fact]
    public async Task SoftDelete_ReferencedByDutyRule_IsBlocked()
    {
        var (def, admin, _) = await SeedAsync();

        var duty = new Duty { Name = "Reading", IsDeleted = false };
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

        _db.DutyRules.Add(new DutyRule
        {
            DutyId = duty.DutyId,
            ServiceDefId = def,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "R",
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            def, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (def, admin, _) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            def, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }
}
