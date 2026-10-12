using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigurationLifecycleService.
/// </summary>
/// <remarks>
/// Covers the audit-write mechanism alone: valid write, invalid
/// action, missing fields, reason trimming, and that nothing else
/// is written. Policy enforcement (E3) and cascades (E4) are not
/// exercised here.
/// </remarks>
public class ConfigurationLifecycleServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, admin, member, member_status, role, " +
            "branch, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<(int adminId, int approverId)> SeedAdminsAsync()
    {
        var role = new MknMs.Domain.D1_RoleDuty.Role
        {
            Name = "R", IsActive = true, IsDeleted = false,
        };
        var branch = new MknMs.Domain.D3_BranchTimeSlotService.Branch
        {
            Name = "B", Location = "L", IsActive = true, IsDeleted = false,
        };
        var status = new MknMs.Domain.D4_Member.MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true,
            IsActive = true, IsDeleted = false,
        };
        var tier = new MknMs.Domain.D10_ConfigLookups.PermissionTier
        {
            Name = "Full Admin", IsDefault = true,
            IsActive = true, IsDeleted = false,
        };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        await _db.SaveChangesAsync();

        var m1 = new MknMs.Domain.D4_Member.Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId, IsActive = true,
        };
        var m2 = new MknMs.Domain.D4_Member.Member
        {
            Name = "A", Surname = "Two",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId, IsActive = true,
        };
        _db.Members.Add(m1);
        _db.Members.Add(m2);
        await _db.SaveChangesAsync();

        var a1 = new MknMs.Domain.D4_Member.Admin
        {
            MemberId = m1.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        var a2 = new MknMs.Domain.D4_Member.Admin
        {
            MemberId = m2.MemberId,
            PermissionTierId = tier.PermissionTierId,
            IsActive = true,
        };
        _db.Admins.Add(a1);
        _db.Admins.Add(a2);
        await _db.SaveChangesAsync();

        return (a1.AdminId, a2.AdminId);
    }

    private static ConfigurationLifecycleEvent ValidEvent(
        int initiatedBy,
        int approvedBy,
        string action = ConfigurationLifecycleActions.Deactivate,
        string? reason = null) =>
        new()
        {
            EntityType = "Role",
            EntityId = 42,
            EntityName = "Coordinator",
            Action = action,
            Reason = reason,
            InitiatedByAdminId = initiatedBy,
            ApprovedByAdminId = approvedBy,
            ConsequencesPreviewed = "1 member holds this role; all fall back.",
            ConsequencesOccurred = "1 member fell back to the default role.",
        };

    [Fact]
    public async Task Record_WithValidDeactivate_WritesRow()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var result = await service.RecordAsync(
            ValidEvent(initiated, approved));

        result.IsSuccess.Should().BeTrue();
        result.Value!.AuditId.Should().BeGreaterThan(0);

        var row = await _db.ConfigurationAuditLogs.SingleAsync();
        row.EntityType.Should().Be("Role");
        row.EntityId.Should().Be(42);
        row.EntityName.Should().Be("Coordinator");
        row.Action.Should().Be(ConfigurationLifecycleActions.Deactivate);
        row.Reason.Should().BeNull();
        row.InitiatedByAdminId.Should().Be(initiated);
        row.ApprovedByAdminId.Should().Be(approved);
        row.InitiatedAt.Should().Be(Now);
        row.ApprovedAt.Should().Be(Now);
        row.NotifiedAt.Should().BeNull();
    }

    [Fact]
    public async Task Record_WithValidSoftDeleteAndReason_WritesRow()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var result = await service.RecordAsync(ValidEvent(
            initiated, approved,
            ConfigurationLifecycleActions.SoftDelete,
            "no longer used"));

        result.IsSuccess.Should().BeTrue();

        var row = await _db.ConfigurationAuditLogs.SingleAsync();
        row.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        row.Reason.Should().Be("no longer used");
    }

    [Fact]
    public async Task Record_TrimsReason()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        await service.RecordAsync(ValidEvent(
            initiated, approved,
            ConfigurationLifecycleActions.SoftDelete,
            "   spaced   "));

        var row = await _db.ConfigurationAuditLogs.SingleAsync();
        row.Reason.Should().Be("spaced");
    }

    [Fact]
    public async Task Record_WithUnknownAction_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var result = await service.RecordAsync(ValidEvent(
            initiated, approved, action: "Explode"));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);

        var count = await _db.ConfigurationAuditLogs.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_WithMissingEntityName_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var e = ValidEvent(initiated, approved) with { EntityName = "" };
        var result = await service.RecordAsync(e);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Record_WithMissingInitiatedBy_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var e = ValidEvent(initiated, approved) with { InitiatedByAdminId = 0 };
        var result = await service.RecordAsync(e);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Record_DoesNotWriteAnythingElse()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var rolesBefore = await _db.Roles.CountAsync();
        var policiesBefore = await _db.EntityDeletionPolicies.CountAsync();

        await service.RecordAsync(ValidEvent(initiated, approved));

        var rolesAfter = await _db.Roles.CountAsync();
        var policiesAfter = await _db.EntityDeletionPolicies.CountAsync();

        rolesAfter.Should().Be(rolesBefore);
        policiesAfter.Should().Be(policiesBefore);
    }
}
