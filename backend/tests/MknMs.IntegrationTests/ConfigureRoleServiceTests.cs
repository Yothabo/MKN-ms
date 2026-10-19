using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureRole;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureRoleService.SoftDeleteAsync.
/// </summary>
public class ConfigureRoleServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int defaultRoleId, int otherRoleId, int actingAdminId)> SeedAsync()
    {
        var defaultRole = new Role
        {
            Name = "Member", IsDefault = true, IsDeleted = false,
        };
        var otherRole = new Role
        {
            Name = "Coordinator", IsDeleted = false,
        };
        var branch = new Branch
        {
            Name = "B", Location = "L", IsDeleted = false,
        };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        Db.Roles.Add(defaultRole);
        Db.Roles.Add(otherRole);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
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
            BranchId = branch.BranchId, RoleId = otherRole.RoleId,
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

        return (defaultRole.RoleId, otherRole.RoleId, admin.AdminId);
    }

    private ConfigureRoleService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var (_, other, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            other, admin, admin, true, reason: "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (_, other, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            other, admin, admin, consequencesAcknowledged: false,
            reason: "no longer used");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_DefaultRole_IsBlocked()
    {
        var (defaultRole, _, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            defaultRole, admin, admin, true, reason: "no longer used");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
        result.Error.Message.Should().Contain("default Role");
    }

    [Fact]
    public async Task SoftDelete_NonDefault_WritesEverythingInOneAtomicCommit()
    {
        var (defaultRole, other, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            other, admin, admin, true, reason: "merged into default");

        result.IsSuccess.Should().BeTrue();

        var role = await Db.Roles.FirstAsync(r => r.RoleId == other);
        role.IsDeleted.Should().BeTrue();
        role.IsActive.Should().BeFalse();

        var member = await Db.Members.FirstAsync();
        member.RoleId.Should().Be(defaultRole);

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.EntityType.Should().Be("Role");
        audit.Reason.Should().Be("merged into default");
        audit.ConsequencesPreviewed.Should().Contain("fell back");
    }

    [Fact]
    public async Task SoftDelete_AlreadyDeleted_Fails()
    {
        var (_, other, admin) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(other, admin, admin, true, "one");
        var second = await service.SoftDeleteAsync(other, admin, admin, true, "two");

        second.IsSuccess.Should().BeFalse();
        second.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyInactive);
    }

    [Fact]
    public async Task SoftDelete_BlockedLeavesNothingChanged()
    {
        var (defaultRole, _, admin) = await SeedAsync();
        var service = MakeService();

        var before = await Db.ConfigurationAuditLogs.CountAsync();

        await service.SoftDeleteAsync(
            defaultRole, admin, admin, true, "attempt");

        Db.ChangeTracker.Clear();

        var after = await Db.ConfigurationAuditLogs.CountAsync();
        after.Should().Be(before);

        var persisted = await Db.Roles
            .AsNoTracking()
            .FirstAsync(r => r.RoleId == defaultRole);
        persisted.IsDeleted.Should().BeFalse();
        persisted.IsActive.Should().BeTrue();
    }
    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        var (_, _, actingAdminId) = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.CreateAsync("NewRole", actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
    [Fact]
    public async Task List_ExcludesSoftDeleted()
    {
        var (_, otherRoleId, actingAdminId) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            otherRoleId, actingAdminId, actingAdminId, true, "test");

        var roles = await service.ListAsync();
        roles.Should().NotContain(r => r.RoleId == otherRoleId);
    }

    [Fact]
    public async Task List_IncludeInactive_ExcludesSoftDeleted()
    {
        var (_, otherRoleId, actingAdminId) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            otherRoleId, actingAdminId, actingAdminId, true, "test");

        var roles = await service.ListAsync(includeInactive: true);
        roles.Should().NotContain(r => r.RoleId == otherRoleId);
    }

    [Fact]
    public async Task Get_SoftDeleted_ReturnsNull()
    {
        var (_, otherRoleId, actingAdminId) = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            otherRoleId, actingAdminId, actingAdminId, true, "test");

        var role = await service.GetAsync(otherRoleId);
        role.Should().BeNull();
    }
}
