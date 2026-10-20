using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageNotificationSubscription;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageNotificationSubscriptionService.
/// </summary>
public class ManageNotificationSubscriptionServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 18, 12, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private sealed record Fixture(int AdminId, int TierId);

    private async Task<Fixture> SeedAsync()
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
            IsActive = true,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "NotificationSubscription",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        return new Fixture(admin.AdminId, tier.PermissionTierId);
    }

    private ManageNotificationSubscriptionService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task Create_WithTier_Succeeds()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync(
            "Deactivate", f.TierId, null, f.AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.EventType.Should().Be("Deactivate");
        result.Value.RecipientTierId.Should().Be(f.TierId);
        result.Value.RecipientAdminId.Should().BeNull();
        result.Value.IsActive.Should().BeTrue();
        result.Value.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithAdmin_Succeeds()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync(
            "SoftDelete", null, f.AdminId, f.AdminId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.RecipientAdminId.Should().Be(f.AdminId);
        result.Value.RecipientTierId.Should().BeNull();
    }

    [Fact]
    public async Task Create_WithNeitherRecipient_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync("Deactivate", null, null, f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Create_WithBothRecipients_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync(
            "Deactivate", f.TierId, f.AdminId, f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Create_WithUnknownEventType_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync(
            "Something", f.TierId, null, f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        var f = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.CreateAsync(
            "Deactivate", f.TierId, null, f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task List_ExcludesSoftDeletedAndFiltersInactive()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.CreateAsync("Deactivate", f.TierId, null, f.AdminId);
        await service.CreateAsync("SoftDelete", null, f.AdminId, f.AdminId);

        var inactive = await service.CreateAsync(
            "Deactivate", f.TierId, null, f.AdminId);
        await service.DeactivateAsync(
            inactive.Value!.SubscriptionId, f.AdminId, f.AdminId, true, null);

        var activeList = await service.ListAsync();
        activeList.Should().HaveCount(2);

        var fullList = await service.ListAsync(includeInactive: true);
        fullList.Should().HaveCount(3);
    }

    [Fact]
    public async Task Deactivate_SetsIsActiveFalseAndWritesAudit()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var created = await service.CreateAsync("Deactivate", f.TierId, null, f.AdminId);
        var result = await service.DeactivateAsync(
            created.Value!.SubscriptionId, f.AdminId, f.AdminId, true, "test");

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.NotificationSubscriptions.AsNoTracking()
            .FirstAsync(s => s.SubscriptionId == created.Value.SubscriptionId);
        persisted.IsActive.Should().BeFalse();
        persisted.IsDeleted.Should().BeFalse();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.Deactivate);
    }

    [Fact]
    public async Task SoftDelete_SetsFlagsAndWritesAudit()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var created = await service.CreateAsync("Deactivate", f.TierId, null, f.AdminId);
        var result = await service.SoftDeleteAsync(
            created.Value!.SubscriptionId, f.AdminId, f.AdminId, true, "retired");

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.NotificationSubscriptions.AsNoTracking()
            .FirstAsync(s => s.SubscriptionId == created.Value.SubscriptionId);
        persisted.IsDeleted.Should().BeTrue();
        persisted.IsActive.Should().BeFalse();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.ConsequencesPreviewed.Should().Contain("No dependent changes apply");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var created = await service.CreateAsync("Deactivate", f.TierId, null, f.AdminId);
        var result = await service.SoftDeleteAsync(
            created.Value!.SubscriptionId, f.AdminId, f.AdminId, false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }
}
