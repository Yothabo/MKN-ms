using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureDuty;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureDutyService.SoftDeleteAsync.
/// </summary>
public class ConfigureDutyServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, service_definition_duty, " +
            "service_definition, service_type, event_duty, program_item, " +
            "program, event, member, member_status, branch, role, duty, " +
            "admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(int dutyId, int adminId)> SeedAsync()
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
        var duty = new Duty { Name = "Reading", IsDeleted = false };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Duty",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
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

        return (duty.DutyId, admin.AdminId);
    }

    private ConfigureDutyService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_Unreferenced_Succeeds()
    {
        var (duty, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            duty, admin, admin, true, "no longer required");

        result.IsSuccess.Should().BeTrue();

        var row = await _db.Duties.FirstAsync(d => d.DutyId == duty);
        row.IsDeleted.Should().BeTrue();
        row.IsActive.Should().BeFalse();

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.EntityType.Should().Be("Duty");
        audit.Reason.Should().Be("no longer required");
    }

    [Fact]
    public async Task SoftDelete_Referenced_IsBlocked()
    {
        var (duty, admin) = await SeedAsync();

        var serviceType = new ServiceType { Name = "Regular" };
        _db.ServiceTypes.Add(serviceType);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = serviceType.ServiceTypeId, IsDeleted = false,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty,
            RequiredSlotCount = 1,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            duty, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        _db.ChangeTracker.Clear();
        var persisted = await _db.Duties.AsNoTracking().FirstAsync(d => d.DutyId == duty);
        persisted.IsDeleted.Should().BeFalse();

        var auditCount = await _db.ConfigurationAuditLogs.CountAsync();
        auditCount.Should().Be(0);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var (duty, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            duty, admin, admin, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (duty, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            duty, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_AlreadyDeleted_Fails()
    {
        var (duty, admin) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(duty, admin, admin, true, "one");
        var second = await service.SoftDeleteAsync(duty, admin, admin, true, "two");

        second.IsSuccess.Should().BeFalse();
        second.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyInactive);
    }
    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        var (_, actingAdminId) = await SeedAsync();
        var admin = await _db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await _db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.CreateAsync("NewDuty", actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
}
