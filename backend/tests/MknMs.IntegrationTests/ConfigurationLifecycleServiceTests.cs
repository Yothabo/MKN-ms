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
            Name = "R", IsDeleted = false,
        };
        var branch = new MknMs.Domain.D3_BranchTimeSlotService.Branch
        {
            Name = "B", Location = "L", IsDeleted = false,
        };
        var status = new MknMs.Domain.D4_Member.MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var tier = new MknMs.Domain.D10_ConfigLookups.PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
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
            MemberStatusId = status.MemberStatusId,
        };
        var m2 = new MknMs.Domain.D4_Member.Member
        {
            Name = "A", Surname = "Two",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId,
        };
        _db.Members.Add(m1);
        _db.Members.Add(m2);
        await _db.SaveChangesAsync();

        var a1 = new MknMs.Domain.D4_Member.Admin
        {
            MemberId = m1.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        var a2 = new MknMs.Domain.D4_Member.Admin
        {
            MemberId = m2.MemberId,
            PermissionTierId = tier.PermissionTierId,
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
        };

    [Fact]
    public async Task Record_WithValidDeactivate_WritesRow()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var result = await service.RecordAsync(
            ValidEvent(initiated, approved));
        await _db.SaveChangesAsync();

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
        // The preview is generated by the service; the caller does
        // not supply it.
        row.ConsequencesPreviewed.Should().Contain("Role");
        row.ConsequencesPreviewed.Should().Contain("Coordinator");
        row.ConsequencesOccurred.Should().Be(row.ConsequencesPreviewed);
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
        await _db.SaveChangesAsync();

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
        await _db.SaveChangesAsync();

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
    public async Task Record_SoftDeleteWithoutReason_Fails_WhenPolicyRequires()
    {
        var (initiated, approved) = await SeedAdminsAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(_db, _clock);
        var result = await service.RecordAsync(ValidEvent(
            initiated, approved,
            ConfigurationLifecycleActions.SoftDelete,
            reason: null));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Record_DeactivateWithReasonRequired_Fails_WhenPolicyRequires()
    {
        var (initiated, approved) = await SeedAdminsAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(_db, _clock);
        var result = await service.RecordAsync(ValidEvent(
            initiated, approved,
            ConfigurationLifecycleActions.Deactivate,
            reason: null));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Record_ApprovalRequired_ApproverEqualsInitiator_Fails()
    {
        var (initiated, _) = await SeedAdminsAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = true,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(_db, _clock);
        var result = await service.RecordAsync(ValidEvent(
            initiatedBy: initiated,
            approvedBy: initiated));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task Record_ApprovalRequired_ApproverHoldsRequiredTier_Succeeds()
    {
        var (initiated, approved) = await SeedAdminsAsync();

        // Give the approver a tier that matches the policy's required
        // tier. SeedAdminsAsync creates both admins with the same tier.
        var tierId = await _db.Admins
            .Where(a => a.AdminId == approved)
            .Select(a => a.PermissionTierId)
            .FirstAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = true,
            RequiredDeactivatePermissionTierId = tierId,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(_db, _clock);
        var result = await service.RecordAsync(ValidEvent(initiated, approved));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Record_ApprovalRequired_ApproverLacksRequiredTier_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();

        // Create a second tier the approver does not hold, and set the
        // policy's required tier to that.
        var otherTier = new MknMs.Domain.D10_ConfigLookups.PermissionTier
        {
            Name = "Other",
            IsDefault = false,
            IsDeleted = false,
        };
        _db.PermissionTiers.Add(otherTier);
        await _db.SaveChangesAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = true,
            RequiredDeactivatePermissionTierId = otherTier.PermissionTierId,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(_db, _clock);
        var result = await service.RecordAsync(ValidEvent(initiated, approved));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);
    }

    [Fact]
    public async Task Record_SoftDeleteWithoutApprovalUsesActingAdmin()
    {
        var (initiated, _) = await SeedAdminsAsync();

        // Policy has no approval requirement; the acting admin is
        // recorded as both initiator and approver.
        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(_db, _clock);
        var result = await service.RecordAsync(ValidEvent(
            initiatedBy: initiated,
            approvedBy: initiated,
            ConfigurationLifecycleActions.SoftDelete,
            reason: "no longer used"));

        result.IsSuccess.Should().BeTrue();
    }

    [Fact]
    public async Task Record_PreviewIsServiceGenerated()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        await service.RecordAsync(ValidEvent(initiated, approved));
        await _db.SaveChangesAsync();

        var row = await _db.ConfigurationAuditLogs.SingleAsync();
        row.ConsequencesPreviewed.Should().Contain("Role");
        row.ConsequencesPreviewed.Should().Contain("Coordinator");
        row.ConsequencesPreviewed.Should().Contain("deactivated");
    }

    [Fact]
    public async Task Record_DoesNotPersist_UntilCallerSaves()
    {
        // §9.7 + §9.8: the state change and the audit write are one
        // atomic unit. RecordAsync only adds the entity; the caller
        // controls the persistence boundary. A read from a fresh
        // context before the caller saves shows nothing.
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        await service.RecordAsync(ValidEvent(initiated, approved));

        // Same context, but explicitly clear the tracker and re-query
        // without saving. The row was added but never committed, so a
        // fresh context sees no row.
        _db.ChangeTracker.Clear();

        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;
        await using var fresh = new MknDbContext(options);
        var count = await fresh.ConfigurationAuditLogs.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_DoesNotWriteAnythingElse()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(_db, _clock);

        var rolesBefore = await _db.Roles.CountAsync();
        var policiesBefore = await _db.EntityDeletionPolicies.CountAsync();

        await service.RecordAsync(ValidEvent(initiated, approved));
        await _db.SaveChangesAsync();

        var rolesAfter = await _db.Roles.CountAsync();
        var policiesAfter = await _db.EntityDeletionPolicies.CountAsync();

        rolesAfter.Should().Be(rolesBefore);
        policiesAfter.Should().Be(policiesBefore);
    }
}
