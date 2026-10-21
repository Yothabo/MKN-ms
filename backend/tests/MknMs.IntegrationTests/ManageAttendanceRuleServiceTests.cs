using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageAttendanceRule;
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
/// Tests for ManageAttendanceRuleService, focused on the three
/// lifecycle mechanisms §9.7 distinguishes for AttendanceRule:
/// Disable (Enabled), Deactivate (IsActive), Soft-delete (IsDeleted
/// + scope-row removal).
/// </summary>
public class ManageAttendanceRuleServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<int> SeedAdminAsync()
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

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "AttendanceRule",
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

        return admin.AdminId;
    }

    private ManageAttendanceRuleService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    private async Task<int> SeedRuleWithScopeAsync(int adminId)
    {
        var rule = new AttendanceRule
        {
            Name = "absent",
            TriggerType = "AbsenceDays",
            TriggerValue = "90",
            OutcomeType = "Notify",
            OutcomeStatusId = null,
            Enabled = true,
            IsActive = true,
            IsDeleted = false,
        };
        Db.AttendanceRules.Add(rule);
        await Db.SaveChangesAsync();

        Db.AttendanceRuleScopes.Add(new AttendanceRuleScope
        {
            AttendanceRuleId = rule.AttendanceRuleId,
            CriteriaType = "Role",
            CriteriaValue = "R",
        });
        await Db.SaveChangesAsync();

        return rule.AttendanceRuleId;
    }

    // -----------------------------------------------------------------
    // Disable / Enable
    // -----------------------------------------------------------------

    [Fact]
    public async Task Disable_SetsEnabledFalse_LeavesScopeAndIsActive()
    {
        var admin = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(admin);
        var service = MakeService();

        var result = await service.DisableAsync(ruleId);

        result.IsSuccess.Should().BeTrue();

        var rule = await Db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.Enabled.Should().BeFalse();
        rule.IsActive.Should().BeTrue();
        rule.IsDeleted.Should().BeFalse();

        var scopes = await Db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(1);

        // Disable is not a §9.7 lifecycle operation: no audit row.
        var audits = await Db.ConfigurationAuditLogs.CountAsync();
        audits.Should().Be(0);
    }

    [Fact]
    public async Task Enable_SetsEnabledTrue_LeavesScopeAndIsActive()
    {
        var admin = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(admin);
        var service = MakeService();

        await service.DisableAsync(ruleId);
        var result = await service.EnableAsync(ruleId);

        result.IsSuccess.Should().BeTrue();

        var rule = await Db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.Enabled.Should().BeTrue();
        rule.IsActive.Should().BeTrue();

        var scopes = await Db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(1);

        var audits = await Db.ConfigurationAuditLogs.CountAsync();
        audits.Should().Be(0);
    }

    // -----------------------------------------------------------------
    // Deactivate
    // -----------------------------------------------------------------

    [Fact]
    public async Task Deactivate_SetsIsActiveFalse_LeavesScopeAndEnabled()
    {
        var admin = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(admin);
        var service = MakeService();

        var result = await service.DeactivateAsync(
            ruleId, admin, admin, true, "paused");

        result.IsSuccess.Should().BeTrue();

        var rule = await Db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.IsActive.Should().BeFalse();
        rule.Enabled.Should().BeTrue();
        rule.IsDeleted.Should().BeFalse();

        var scopes = await Db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(1);

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.Deactivate);
    }

    // -----------------------------------------------------------------
    // Soft delete
    // -----------------------------------------------------------------

    [Fact]
    public async Task SoftDelete_SetsFlags_AndRemovesScopeRows()
    {
        var admin = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(admin);
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            ruleId, admin, admin, true, "no longer used");

        result.IsSuccess.Should().BeTrue();

        var rule = await Db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.IsDeleted.Should().BeTrue();
        rule.IsActive.Should().BeFalse();

        var scopes = await Db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(0);

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var admin = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(admin);
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            ruleId, admin, admin, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        // C6: the acting admin must resolve to an Admin row whose
        // IsActive = true.
        var adminId = await SeedAdminAsync();

        var admin = await Db.Admins.FirstAsync(a => a.AdminId == adminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();

        var result = await service.CreateAsync(new CreateAttendanceRuleRequest
        {
            Name = "rule",
            TriggerType = "AbsenceDays",
            TriggerValue = "90",
            OutcomeType = "Notify",
            OutcomeStatusId = null,
            Enabled = true,
            ScopeCriteria = null,
            ActingAdminId = adminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task Reactivate_DeactivatedRule_Succeeds()
    {
        // §9.7: deactivation is reversible.
        var adminId = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(adminId);
        var service = MakeService();

        var deactivated = await service.DeactivateAsync(
            ruleId, adminId, adminId, true, "paused");
        deactivated.IsSuccess.Should().BeTrue();

        var auditBefore = await Db.ConfigurationAuditLogs.CountAsync();

        var result = await service.ReactivateAsync(ruleId, adminId);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.AttendanceRules
            .AsNoTracking()
            .FirstAsync(r => r.AttendanceRuleId == ruleId);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();

        // Reactivation does not touch the Enabled flag or the scope rows.
        var scopes = await Db.AttendanceRuleScopes
            .CountAsync(s => s.AttendanceRuleId == ruleId);
        scopes.Should().Be(1);

        var auditAfter = await Db.ConfigurationAuditLogs.CountAsync();
        auditAfter.Should().Be(auditBefore);
    }

    [Fact]
    public async Task Reactivate_SoftDeletedRule_Fails()
    {
        var adminId = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(adminId);
        var service = MakeService();

        await service.SoftDeleteAsync(
            ruleId, adminId, adminId, true, "test");

        var result = await service.ReactivateAsync(ruleId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        var persisted = await Db.AttendanceRules
            .AsNoTracking()
            .FirstAsync(r => r.AttendanceRuleId == ruleId);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveRule_Fails()
    {
        var adminId = await SeedAdminAsync();
        var ruleId = await SeedRuleWithScopeAsync(adminId);
        var service = MakeService();

        var result = await service.ReactivateAsync(ruleId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyActive);
    }
}
