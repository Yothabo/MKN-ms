using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ConfigureDutyRules;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigureDutyRulesService.ReactivateAsync.
/// </summary>
/// <remarks>
/// Covers reactivation only. Create, Update, Deactivate, and List are
/// not exercised by this file at the current phase — they are
/// pre-existing behaviour and were covered by the review's acceptance
/// matrix only through the process-level tests, not by a dedicated
/// class. The reactivation facts are added here to close the §9.7
/// reversible-deactivation gap that the code did not previously
/// implement.
/// </remarks>
public class ConfigureDutyRulesServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 21, 12, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private async Task<(int ruleId, int adminId)> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var duty = new Duty { Name = "Reading", IsDeleted = false };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "DutyRule",
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

        var rule = new MknMs.Domain.D2_DutyRule.DutyRule
        {
            DutyId = duty.DutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "R",
            IsActive = true,
            IsDeleted = false,
        };
        Db.DutyRules.Add(rule);
        await Db.SaveChangesAsync();

        return (rule.RuleId, admin.AdminId);
    }

    private ConfigureDutyRulesService MakeService() =>
        new(Db, new ConfigurationLifecycleService(Db, _clock));

    [Fact]
    public async Task Reactivate_DeactivatedRule_Succeeds()
    {
        // §9.7: deactivation is reversible.
        var (ruleId, adminId) = await SeedAsync();
        var service = MakeService();

        var deactivated = await service.DeactivateAsync(
            ruleId, adminId, adminId, true, "test");
        deactivated.IsSuccess.Should().BeTrue();

        var auditBefore = await Db.ConfigurationAuditLogs.CountAsync();

        var result = await service.ReactivateAsync(ruleId, adminId);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.DutyRules
            .AsNoTracking()
            .FirstAsync(r => r.RuleId == ruleId);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();

        var auditAfter = await Db.ConfigurationAuditLogs.CountAsync();
        auditAfter.Should().Be(auditBefore);
    }

    [Fact]
    public async Task Reactivate_SoftDeletedRule_Fails()
    {
        // DutyRule can reach IsDeleted = true via a Role or AttributeType
        // cascade even though this service exposes no direct soft delete.
        // The guard refuses reactivation from that state.
        var (ruleId, adminId) = await SeedAsync();

        var rule = await Db.DutyRules.FirstAsync(r => r.RuleId == ruleId);
        rule.IsActive = false;
        rule.IsDeleted = true;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.ReactivateAsync(ruleId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        var persisted = await Db.DutyRules
            .AsNoTracking()
            .FirstAsync(r => r.RuleId == ruleId);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveRule_Fails()
    {
        var (ruleId, adminId) = await SeedAsync();
        var service = MakeService();

        var result = await service.ReactivateAsync(ruleId, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyActive);
    }
}
