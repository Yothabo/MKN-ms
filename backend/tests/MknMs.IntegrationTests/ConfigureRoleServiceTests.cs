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
public class ConfigureRoleServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 10, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, duty_rule, member, member_status, " +
            "branch, role, duty, admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(int defaultRoleId, int otherRoleId, int actingAdminId)> SeedAsync()
    {
        var defaultRole = new Role
        {
            Name = "Member", IsDefault = true,
            IsActive = true, IsDeleted = false,
        };
        var otherRole = new Role
        {
            Name = "Coordinator",
            IsActive = true, IsDeleted = false,
        };
        var branch = new Branch
        {
            Name = "B", Location = "L", IsActive = true, IsDeleted = false,
        };
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

        _db.Roles.Add(defaultRole);
        _db.Roles.Add(otherRole);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        await _db.SaveChangesAsync();

        var member = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = otherRole.RoleId,
            MemberStatusId = status.MemberStatusId, IsActive = true,
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return (defaultRole.RoleId, otherRole.RoleId, admin.AdminId);
    }

    private ConfigureRoleService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

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

        var role = await _db.Roles.FirstAsync(r => r.RoleId == other);
        role.IsDeleted.Should().BeTrue();
        role.IsActive.Should().BeFalse();

        var member = await _db.Members.FirstAsync();
        member.RoleId.Should().Be(defaultRole);

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
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

        var before = await _db.ConfigurationAuditLogs.CountAsync();

        await service.SoftDeleteAsync(
            defaultRole, admin, admin, true, "attempt");

        _db.ChangeTracker.Clear();

        var after = await _db.ConfigurationAuditLogs.CountAsync();
        after.Should().Be(before);

        var persisted = await _db.Roles
            .AsNoTracking()
            .FirstAsync(r => r.RoleId == defaultRole);
        persisted.IsDeleted.Should().BeFalse();
        persisted.IsActive.Should().BeTrue();
    }
}
