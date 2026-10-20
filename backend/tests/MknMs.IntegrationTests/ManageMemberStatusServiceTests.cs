using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageMemberStatus;
using MknMs.Application.Processes.Configuration.ConfigureRole;
using MknMs.Application.Processes.Configuration.ConfigureBranch;
using MknMs.Application.Processes.Configuration.ManageMemberRecord;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D13_AttendanceRule;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageMemberStatusService.SoftDeleteAsync and the
/// MemberStatus cascade.
/// </summary>
public class ManageMemberStatusServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 14, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private sealed record Fixture(
        int DefaultStatusId,
        int OtherStatusId,
        int MemberId,
        int AdminId,
        int ScopedRuleId,
        int OutcomeRuleId);

    private async Task<Fixture> SeedAsync()
    {
        var defaultStatusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var otherStatus = new MemberStatus
        {
            Name = "Inactive", IsRosterable = false, IsDefault = false, IsDeleted = false,
        };
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        Db.MemberStatuses.Add(otherStatus);
        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "MemberStatus",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        // Residual 2: the two new facts soft-delete a Role and a Branch
        // to exercise the member-write guard. Without a policy row for
        // each entity type, RecordAsync refuses with
        // ConfigurationMissing before the state check is reached.
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


        var member = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = otherStatus.MemberStatusId,
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

        // A rule whose OutcomeStatusId names the deleted status.
        var outcomeRule = new AttendanceRule
        {
            Name = "Outcome rule",
            TriggerType = "Manual",
            TriggerValue = null,
            OutcomeType = "SetStatus",
            OutcomeStatusId = otherStatus.MemberStatusId,
            Enabled = true,
            IsDeleted = false,
        };
        // A rule whose scope criterion names the deleted status.
        var scopedRule = new AttendanceRule
        {
            Name = "Scoped rule",
            TriggerType = "Manual",
            TriggerValue = null,
            OutcomeType = "Notify",
            OutcomeStatusId = null,
            Enabled = true,
            IsDeleted = false,
        };
        Db.AttendanceRules.Add(outcomeRule);
        Db.AttendanceRules.Add(scopedRule);
        await Db.SaveChangesAsync();

        Db.AttendanceRuleScopes.Add(new AttendanceRuleScope
        {
            AttendanceRuleId = scopedRule.AttendanceRuleId,
            CriteriaType = "MemberStatus",
            CriteriaValue = $"1,{otherStatus.MemberStatusId},2",
        });
        await Db.SaveChangesAsync();

        return new Fixture(
            defaultStatusId,
            otherStatus.MemberStatusId,
            member.MemberId,
            admin.AdminId,
            scopedRule.AttendanceRuleId,
            outcomeRule.AttendanceRuleId);
    }

    private ManageMemberStatusService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_DefaultStatus_IsBlocked()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.DefaultStatusId, f.AdminId, f.AdminId, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        Db.ChangeTracker.Clear();
        var persisted = await Db.MemberStatuses
            .AsNoTracking()
            .FirstAsync(s => s.MemberStatusId == f.DefaultStatusId);
        persisted.IsDeleted.Should().BeFalse();

        var audit = await Db.ConfigurationAuditLogs.CountAsync();
        audit.Should().Be(0);
    }

    [Fact]
    public async Task SoftDelete_NonDefault_FallsBackMembers()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId, true, "no longer used");

        result.IsSuccess.Should().BeTrue();

        var member = await Db.Members.FirstAsync(m => m.MemberId == f.MemberId);
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task SoftDelete_NonDefault_PhysicallyRemovesOutcomeRule()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId, true, "no longer used");

        var exists = await Db.AttendanceRules
            .AnyAsync(r => r.AttendanceRuleId == f.OutcomeRuleId);
        exists.Should().BeFalse();
    }

    [Fact]
    public async Task SoftDelete_NonDefault_RemovesScopeRowsNamingTheStatus()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId, true, "no longer used");

        var scopes = await Db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == f.ScopedRuleId)
            .ToListAsync();

        // The row named "1,{status},2"; its criterion names the
        // deleted status, so §9.7 removes the whole row.
        scopes.Should().BeEmpty();
    }

    [Fact]
    public async Task SoftDelete_NonDefault_WritesAuditWithCascadeText()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId, true, "no longer used");

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.EntityType.Should().Be("MemberStatus");
        audit.Reason.Should().Be("no longer used");
        audit.ConsequencesPreviewed.Should().Contain("fell back");
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_DoesNotSubstringMatchScopeCriteria()
    {
        // A rule scoped to status 13 must not be removed when status 3
        // is deleted.
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var defaultStatusId2 = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var other = new MemberStatus
        {
            Name = "Other", IsRosterable = false, IsDefault = false, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.MemberStatuses.Add(other);
        Db.PermissionTiers.Add(tier);
        await Db.SaveChangesAsync();

        var adminMember = new Member
        {
            Name = "Admin", Surname = "User",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = defaultStatusId2,
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

        var rule = new AttendanceRule
        {
            Name = "Scoped",
            TriggerType = "Manual",
            OutcomeType = "Notify",
            Enabled = true,
            IsDeleted = false,
        };
        Db.AttendanceRules.Add(rule);
        await Db.SaveChangesAsync();

        Db.AttendanceRuleScopes.Add(new AttendanceRuleScope
        {
            AttendanceRuleId = rule.AttendanceRuleId,
            CriteriaType = "MemberStatus",
            CriteriaValue = "13",
        });
        await Db.SaveChangesAsync();

        var service = MakeService();
        await service.SoftDeleteAsync(
            other.MemberStatusId, admin.AdminId, admin.AdminId, true, "test");

        // The scope row for "13" is untouched.
        Db.ChangeTracker.Clear();
        var scopes = await Db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == rule.AttendanceRuleId)
            .ToListAsync();
        scopes.Should().HaveCount(1);
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId,
            consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }
    [Fact]
    public async Task SetStatus_SoftDeletedStatus_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        // Soft-delete the non-default status. §9.7 requires it to be
        // hidden from ordinary views; a member must not be assigned to
        // it via SetStatusAsync.
        await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId, true, "test");

        var memberService = new ManageMemberRecordService(Db);
        var result = await memberService.SetStatusAsync(f.MemberId, f.OtherStatusId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
    [Fact]
    public async Task Update_WithSoftDeletedRole_Fails()
    {
        var f = await SeedAsync();

        var newRole = new Role { Name = "TempRole", IsActive = true, IsDeleted = false };
        Db.Roles.Add(newRole);
        await Db.SaveChangesAsync();

        var roleService = new ConfigureRoleService(
            Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));
        var roleDelete = await roleService.SoftDeleteAsync(
            newRole.RoleId, f.AdminId, f.AdminId, true, "test");
        roleDelete.IsSuccess.Should().BeTrue(
            "the fixture must seed an EntityDeletionPolicy for Role; without it, the cascade blocks and the test premise fails");

        var member = await Db.Members.FirstAsync(m => m.MemberId == f.MemberId);
        var memberService = new ManageMemberRecordService(Db);
        var result = await memberService.UpdateAsync(
            member.MemberId,
            new UpdateMemberRequest
            {
                Name = member.Name,
                Surname = member.Surname,
                DateOfBirth = member.DateOfBirth,
                MembershipStage = member.MembershipStage,
                Gender = member.Gender,
                Phone = member.Phone,
                Email = null,
                BranchId = member.BranchId,
                RoleId = newRole.RoleId,
                MemberStatusId = f.DefaultStatusId,
            });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task Update_WithSoftDeletedBranch_Fails()
    {
        var f = await SeedAsync();

        var newBranch = new Branch { Name = "TempBranch", Location = "Elsewhere", IsActive = true, IsDeleted = false };
        Db.Branches.Add(newBranch);
        await Db.SaveChangesAsync();

        var branchService = new ConfigureBranchService(
            Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));
        var branchDelete = await branchService.SoftDeleteAsync(
            newBranch.BranchId, f.AdminId, f.AdminId, true, "test");
        branchDelete.IsSuccess.Should().BeTrue(
            "the fixture must seed an EntityDeletionPolicy for Branch; without it, the cascade blocks and the test premise fails");

        var member = await Db.Members.FirstAsync(m => m.MemberId == f.MemberId);
        var memberService = new ManageMemberRecordService(Db);
        var result = await memberService.UpdateAsync(
            member.MemberId,
            new UpdateMemberRequest
            {
                Name = member.Name,
                Surname = member.Surname,
                DateOfBirth = member.DateOfBirth,
                MembershipStage = member.MembershipStage,
                Gender = member.Gender,
                Phone = member.Phone,
                Email = null,
                BranchId = newBranch.BranchId,
                RoleId = member.RoleId,
                MemberStatusId = f.DefaultStatusId,
            });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
}
