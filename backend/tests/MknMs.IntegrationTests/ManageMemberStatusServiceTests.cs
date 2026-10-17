using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageMemberStatus;
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
public class ManageMemberStatusServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 14, 0, 0, TimeSpan.Zero);

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

    private sealed record Fixture(
        int DefaultStatusId,
        int OtherStatusId,
        int MemberId,
        int AdminId,
        int ScopedRuleId,
        int OutcomeRuleId);

    private async Task<Fixture> SeedAsync()
    {
        var defaultStatus = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
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

        _db.MemberStatuses.Add(defaultStatus);
        _db.MemberStatuses.Add(otherStatus);
        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.PermissionTiers.Add(tier);
        await _db.SaveChangesAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "MemberStatus",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();


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
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

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
        _db.AttendanceRules.Add(outcomeRule);
        _db.AttendanceRules.Add(scopedRule);
        await _db.SaveChangesAsync();

        _db.AttendanceRuleScopes.Add(new AttendanceRuleScope
        {
            AttendanceRuleId = scopedRule.AttendanceRuleId,
            CriteriaType = "MemberStatus",
            CriteriaValue = $"1,{otherStatus.MemberStatusId},2",
        });
        await _db.SaveChangesAsync();

        return new Fixture(
            defaultStatus.MemberStatusId,
            otherStatus.MemberStatusId,
            member.MemberId,
            admin.AdminId,
            scopedRule.AttendanceRuleId,
            outcomeRule.AttendanceRuleId);
    }

    private ManageMemberStatusService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_DefaultStatus_IsBlocked()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.DefaultStatusId, f.AdminId, f.AdminId, true, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        _db.ChangeTracker.Clear();
        var persisted = await _db.MemberStatuses
            .AsNoTracking()
            .FirstAsync(s => s.MemberStatusId == f.DefaultStatusId);
        persisted.IsDeleted.Should().BeFalse();

        var audit = await _db.ConfigurationAuditLogs.CountAsync();
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

        var member = await _db.Members.FirstAsync(m => m.MemberId == f.MemberId);
        member.MemberStatusId.Should().Be(f.DefaultStatusId);
    }

    [Fact]
    public async Task SoftDelete_NonDefault_PhysicallyRemovesOutcomeRule()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            f.OtherStatusId, f.AdminId, f.AdminId, true, "no longer used");

        var exists = await _db.AttendanceRules
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

        var scopes = await _db.AttendanceRuleScopes
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

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
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
        var defaultStatus = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var other = new MemberStatus
        {
            Name = "Other", IsRosterable = false, IsDefault = false, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(defaultStatus);
        _db.MemberStatuses.Add(other);
        _db.PermissionTiers.Add(tier);
        await _db.SaveChangesAsync();

        var adminMember = new Member
        {
            Name = "Admin", Surname = "User",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-1", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = defaultStatus.MemberStatusId,
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

        var rule = new AttendanceRule
        {
            Name = "Scoped",
            TriggerType = "Manual",
            OutcomeType = "Notify",
            Enabled = true,
            IsDeleted = false,
        };
        _db.AttendanceRules.Add(rule);
        await _db.SaveChangesAsync();

        _db.AttendanceRuleScopes.Add(new AttendanceRuleScope
        {
            AttendanceRuleId = rule.AttendanceRuleId,
            CriteriaType = "MemberStatus",
            CriteriaValue = "13",
        });
        await _db.SaveChangesAsync();

        var service = MakeService();
        await service.SoftDeleteAsync(
            other.MemberStatusId, admin.AdminId, admin.AdminId, true, "test");

        // The scope row for "13" is untouched.
        _db.ChangeTracker.Clear();
        var scopes = await _db.AttendanceRuleScopes
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
}
