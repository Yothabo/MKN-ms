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
public class ConfigureBranchServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 16, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, branch_time_slot, member, member_status, " +
            "branch, role, admin, permission_tier, time_of_day, event_branch, event " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(int branchId, int adminId)> SeedAsync(bool withMember = false)
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var otherBranch = new Branch { Name = "B2", Location = "L2", IsDeleted = false };
        var status = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var timeOfDay = new TimeOfDay { Name = "Morning" };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.Branches.Add(otherBranch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.TimeOfDays.Add(timeOfDay);
        await _db.SaveChangesAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Branch",
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
            BranchId = otherBranch.BranchId, RoleId = role.RoleId,
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
        await _db.SaveChangesAsync();

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
                MemberStatusId = status.MemberStatusId,
            };
            _db.Members.Add(extra);
            await _db.SaveChangesAsync();
        }

        // Branch time slots referencing the branch.
        _db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        });
        _db.BranchTimeSlots.Add(new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Wednesday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        return (branch.BranchId, admin.AdminId);
    }

    private ConfigureBranchService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_WithMembers_IsBlocked()
    {
        var (branch, admin) = await SeedAsync(withMember: true);
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            branch, admin, admin, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        _db.ChangeTracker.Clear();
        var persisted = await _db.Branches.AsNoTracking().FirstAsync(b => b.BranchId == branch);
        persisted.IsDeleted.Should().BeFalse();

        var slots = await _db.BranchTimeSlots.CountAsync(s => s.BranchId == branch);
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

        var slots = await _db.BranchTimeSlots.CountAsync(s => s.BranchId == branch);
        slots.Should().Be(0);

        var persisted = await _db.Branches.AsNoTracking().FirstAsync(b => b.BranchId == branch);
        persisted.IsDeleted.Should().BeTrue();

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
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
        var admin = await _db.Admins.FirstAsync(a => a.AdminId == actingAdminId);
        admin.IsActive = false;
        await _db.SaveChangesAsync();

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
