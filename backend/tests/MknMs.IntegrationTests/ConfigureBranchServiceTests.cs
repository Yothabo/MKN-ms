using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureBranch;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureBranchService.SoftDeleteAsync and the Branch
/// cascade.
/// </summary>
public class ConfigureBranchServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 16, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int branchId, int adminId)> SeedAsync(bool withMember = false)
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var otherBranch = new Branch { Name = "B2", Location = "L2", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var timeOfDay = new TimeOfDay { Name = "Morning" };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.Branches.Add(otherBranch);
        Db.PermissionTiers.Add(tier);
        Db.TimeOfDays.Add(timeOfDay);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Branch",
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
            BranchId = otherBranch.BranchId, RoleId = role.RoleId,
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

        if (withMember)
        {
            var extra = new Member
            {
                Name = "Extra", Surname = "User",
                JoinDate = new DateOnly(2024, 1, 1),
                DateOfBirth = new DateOnly(1990, 1, 1),
                MembershipStage = "Full", Gender = "Other",
                Phone = "+27-2", Email = null,
                BranchId = branch.BranchId, RoleId = role.RoleId,
                MemberStatusId = statusId,
            };
            Db.Members.Add(extra);
            await Db.SaveChangesAsync();
        }

        // Branch time slots referencing the branch.
        Db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        });
        Db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Wednesday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        return (branch.BranchId, admin.AdminId);
    }

    private ConfigureBranchService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_WithMembers_IsBlocked()
    {
        var (branch, admin) = await SeedAsync(withMember: true);
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        Db.ChangeTracker.Clear();
        var persisted = await Db.Branches.AsNoTracking().FirstAsync(b => b.BranchId == branch);
        persisted.IsDeleted.Should().BeFalse();

        var slots = await Db.BranchTimeSlots.CountAsync(s => s.BranchId == branch);
        slots.Should().Be(2, "a blocked soft delete must not remove slots");
    }

    [Fact]
    public async Task SoftDelete_NoMembers_RemovesTimeSlots()
    {
        var (branch, admin) = await SeedAsync(withMember: false);
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, true, "not used");

        result.IsSuccess.Should().BeTrue();

        var slots = await Db.BranchTimeSlots.CountAsync(s => s.BranchId == branch);
        slots.Should().Be(0);

        var persisted = await Db.Branches.AsNoTracking().FirstAsync(b => b.BranchId == branch);
        persisted.IsDeleted.Should().BeTrue();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var (branch, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var (branch, admin) = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }
    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        var (_, actingAdminId) = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.CreateAsync("NewBranch", "Somewhere", actingAdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
    [Fact]
    public async Task List_ExcludesSoftDeleted()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(branchId, adminId, adminId, true, "test");

        var branches = await service.ListAsync();
        branches.Should().NotContain(b => b.BranchId == branchId);
    }

    [Fact]
    public async Task List_IncludeInactive_ExcludesSoftDeleted()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(branchId, adminId, adminId, true, "test");

        var branches = await service.ListAsync(includeInactive: true);
        branches.Should().NotContain(b => b.BranchId == branchId);
    }

    [Fact]
    public async Task Get_SoftDeleted_ReturnsNull()
    {
        var (branchId, adminId) = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(branchId, adminId, adminId, true, "test");

        var branch = await service.GetAsync(branchId);
        branch.Should().BeNull();
    }
}
