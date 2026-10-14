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

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageAttendanceRuleService, focused on the three
/// lifecycle mechanisms §9.7 distinguishes for AttendanceRule:
/// Disable (Enabled), Deactivate (IsActive), Soft-delete (IsDeleted
/// + scope-row removal).
/// </summary>
public class ManageAttendanceRuleServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 11, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, attendance_rule_scope, attendance_rule, " +
            "member, member_status, branch, role, admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> SeedAdminAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var status = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };

        _db.Roles.Add(role);
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
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = status.MemberStatusId,
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return admin.AdminId;
    }

    private ManageAttendanceRuleService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

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
        _db.AttendanceRules.Add(rule);
        await _db.SaveChangesAsync();

        _db.AttendanceRuleScopes.Add(new AttendanceRuleScope
        {
            AttendanceRuleId = rule.AttendanceRuleId,
            CriteriaType = "Role",
            CriteriaValue = "R",
        });
        await _db.SaveChangesAsync();

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

        var rule = await _db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.Enabled.Should().BeFalse();
        rule.IsActive.Should().BeTrue();
        rule.IsDeleted.Should().BeFalse();

        var scopes = await _db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(1);

        // Disable is not a §9.7 lifecycle operation: no audit row.
        var audits = await _db.ConfigurationAuditLogs.CountAsync();
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

        var rule = await _db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.Enabled.Should().BeTrue();
        rule.IsActive.Should().BeTrue();

        var scopes = await _db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(1);

        var audits = await _db.ConfigurationAuditLogs.CountAsync();
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

        var rule = await _db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.IsActive.Should().BeFalse();
        rule.Enabled.Should().BeTrue();
        rule.IsDeleted.Should().BeFalse();

        var scopes = await _db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(1);

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
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

        var rule = await _db.AttendanceRules
            .AsNoTracking().FirstAsync(r => r.AttendanceRuleId == ruleId);
        rule.IsDeleted.Should().BeTrue();
        rule.IsActive.Should().BeFalse();

        var scopes = await _db.AttendanceRuleScopes
            .Where(s => s.AttendanceRuleId == ruleId).CountAsync();
        scopes.Should().Be(0);

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
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
}
