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
public class ConfigurationLifecycleServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 12, 10, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

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
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new MknMs.Domain.D10_ConfigLookups.PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        await Db.SaveChangesAsync();

        var m1 = new MknMs.Domain.D4_Member.Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        var m2 = new MknMs.Domain.D4_Member.Member
        {
            Name = "A", Surname = "Two",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(m1);
        Db.Members.Add(m2);
        await Db.SaveChangesAsync();

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
        Db.Admins.Add(a1);
        Db.Admins.Add(a2);
        await Db.SaveChangesAsync();

        return (a1.AdminId, a2.AdminId);
    }

    private static ConfigurationLifecycleEvent ValidEvent(
        int initiatedBy,
        int approvedBy,
        string action = ConfigurationLifecycleActions.Deactivate,
        string? reason = null,
        bool acknowledged = true) =>
        new()
        {
            EntityType = "Role",
            EntityId = 42,
            EntityName = "Coordinator",
            Action = action,
            Reason = reason,
            InitiatedByAdminId = initiatedBy,
            ApprovedByAdminId = approvedBy,
            ConsequencesAcknowledged = acknowledged,
        };

    // Matrix row Configuration:f427df30f064: A configuration deactivation or soft delete | A ConfigurationAuditLog row is written
    [Fact]
    [Trait("MatrixRow", "Configuration:f427df30f064")]
    public async Task Record_WithValidDeactivate_WritesRow()
    {
        var (initiated, approved) = await SeedAdminsAsync();
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

        var service = new ConfigurationLifecycleService(Db, _clock);

        var result = await service.RecordAsync(
            ValidEvent(initiated, approved));
        await Db.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();
        result.Value!.AuditId.Should().BeGreaterThan(0);

        var row = await Db.ConfigurationAuditLogs.SingleAsync();
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

        var service = new ConfigurationLifecycleService(Db, _clock);

        var result = await service.RecordAsync(ValidEvent(
            initiated, approved,
            ConfigurationLifecycleActions.SoftDelete,
            "no longer used"));
        await Db.SaveChangesAsync();

        result.IsSuccess.Should().BeTrue();

        var row = await Db.ConfigurationAuditLogs.SingleAsync();
        row.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        row.Reason.Should().Be("no longer used");
    }

    [Fact]
    public async Task Record_TrimsReason()
    {
        var (initiated, approved) = await SeedAdminsAsync();
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

        var service = new ConfigurationLifecycleService(Db, _clock);

        await service.RecordAsync(ValidEvent(
            initiated, approved,
            ConfigurationLifecycleActions.SoftDelete,
            "   spaced   "));
        await Db.SaveChangesAsync();

        var row = await Db.ConfigurationAuditLogs.SingleAsync();
        row.Reason.Should().Be("spaced");
    }

    [Fact]
    public async Task Record_WithUnknownAction_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(Db, _clock);

        var result = await service.RecordAsync(ValidEvent(
            initiated, approved, action: "Explode"));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);

        var count = await Db.ConfigurationAuditLogs.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_WithMissingEntityName_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(Db, _clock);

        var e = ValidEvent(initiated, approved) with { EntityName = "" };
        var result = await service.RecordAsync(e);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Record_WithMissingInitiatedBy_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();
        var service = new ConfigurationLifecycleService(Db, _clock);

        var e = ValidEvent(initiated, approved) with { InitiatedByAdminId = 0 };
        var result = await service.RecordAsync(e);

        result.IsSuccess.Should().BeFalse();
    }

    [Fact]
    public async Task Record_SoftDeleteWithoutReason_Fails_WhenPolicyRequires()
    {
        var (initiated, approved) = await SeedAdminsAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(Db, _clock);
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

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = true,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(Db, _clock);
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

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = true,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(Db, _clock);
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
        var tierId = await Db.Admins
            .Where(a => a.AdminId == approved)
            .Select(a => a.PermissionTierId)
            .FirstAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = true,
            RequiredDeactivatePermissionTierId = tierId,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(Db, _clock);
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
        Db.PermissionTiers.Add(otherTier);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = true,
            RequiredDeactivatePermissionTierId = otherTier.PermissionTierId,
            RequiresReasonForDelete = false,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(Db, _clock);
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
        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Role",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(Db, _clock);
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

        var service = new ConfigurationLifecycleService(Db, _clock);

        await service.RecordAsync(ValidEvent(initiated, approved));
        await Db.SaveChangesAsync();

        var row = await Db.ConfigurationAuditLogs.SingleAsync();
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

        var service = new ConfigurationLifecycleService(Db, _clock);

        await service.RecordAsync(ValidEvent(initiated, approved));

        // Same context, but explicitly clear the tracker and re-query
        // without saving. The row was added but never committed, so a
        // fresh context sees no row.
        Db.ChangeTracker.Clear();

        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;
        await using var fresh = new MknDbContext(options);
        var count = await fresh.ConfigurationAuditLogs.CountAsync();

        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_DoesNotWriteAnythingElse()
    {
        var (initiated, approved) = await SeedAdminsAsync();
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

        var service = new ConfigurationLifecycleService(Db, _clock);

        var rolesBefore = await Db.Roles.CountAsync();
        var policiesBefore = await Db.EntityDeletionPolicies.CountAsync();

        await service.RecordAsync(ValidEvent(initiated, approved));
        await Db.SaveChangesAsync();

        var rolesAfter = await Db.Roles.CountAsync();
        var policiesAfter = await Db.EntityDeletionPolicies.CountAsync();

        rolesAfter.Should().Be(rolesBefore);
        policiesAfter.Should().Be(policiesBefore);
    }

    [Fact]
    public async Task Record_WithInactiveApprover_Fails()
    {
        // C6: the approver must resolve to an Admin row whose
        // IsActive = true.
        var (initiated, approved) = await SeedAdminsAsync();
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

        var approver = await Db.Admins.FirstAsync(a => a.AdminId == approved);
        approver.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new ConfigurationLifecycleService(Db, _clock);
        var result = await service.RecordAsync(ValidEvent(initiated, approved));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task Record_WithNonexistentApprover_Fails_NoTierRequired()
    {
        // C6: the approver lookup runs even when no EntityDeletionPolicy
        // configures a required tier for the operation. An approver id
        // that names no row is rejected.
        var (initiated, _) = await SeedAdminsAsync();
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

        var service = new ConfigurationLifecycleService(Db, _clock);
        var result = await service.RecordAsync(ValidEvent(
            initiatedBy: initiated,
            approvedBy: 9999));

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityMissing);
    }
    [Fact]
    public async Task Record_MissingPolicy_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();

        // No policy is seeded for this entity type. §9.7 requires the
        // policy to govern the operation; the service must refuse.
        var service = new ConfigurationLifecycleService(Db, _clock);
        var e = ValidEvent(initiated, approved) with { EntityType = "UnconfiguredEntity" };

        var result = await service.RecordAsync(e);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ConfigurationMissing);
    }
    // Matrix row Configuration:c2a3988dbf3b: A configuration soft delete whose consequences the admin has not acknowledged | The operation is refused
    [Fact]
    [Trait("MatrixRow", "Configuration:c2a3988dbf3b")]
    public async Task Record_WithoutAcknowledgement_Fails()
    {
        var (initiated, approved) = await SeedAdminsAsync();

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

        var service = new ConfigurationLifecycleService(Db, _clock);
        var e = ValidEvent(initiated, approved, acknowledged: false);

        var result = await service.RecordAsync(e);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }
}
