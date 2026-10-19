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
public class ConfigureDutyServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 12, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int dutyId, int adminId)> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var duty = new Duty { Name = "Reading", IsDeleted = false };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Duty",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
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

        return (duty.DutyId, admin.AdminId);
    }

    private ConfigureDutyService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_Unreferenced_Succeeds()
    {
        var (duty, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            duty, admin, admin, true, "no longer required");

        result.IsSuccess.Should().BeTrue();

        var row = await Db.Duties.FirstAsync(d => d.DutyId == duty);
        row.IsDeleted.Should().BeTrue();
        row.IsActive.Should().BeFalse();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.EntityType.Should().Be("Duty");
        audit.Reason.Should().Be("no longer required");
    }

    [Fact]
    public async Task SoftDelete_Referenced_IsBlocked()
    {
        var (duty, admin) = await SeedAsync();

        var serviceType = new ServiceType { Name = "Regular" };
        Db.ServiceTypes.Add(serviceType);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = serviceType.ServiceTypeId, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty,
            RequiredSlotCount = 1,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.SoftDeleteAsync(
            duty, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        Db.ChangeTracker.Clear();
        var persisted = await Db.Duties.AsNoTracking().FirstAsync(d => d.DutyId == duty);
        persisted.IsDeleted.Should().BeFalse();

        var auditCount = await Db.ConfigurationAuditLogs.CountAsync();
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
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.CreateAsync("NewDuty", actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
}
