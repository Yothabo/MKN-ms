using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ConfigurationCascadeService.
/// </summary>
public class ConfigurationCascadeServiceTests : IntegrationTestBase
{
    protected override async Task OnInitializedAsync()
    {
        // The seed in each fact inserts a default Role and a default
        // MemberStatus. The per-class database persists across facts,
        // and Role_NonDefault_WithoutDefaultRole_FailsConfigurationMissing
        // requires that no default Role exist at all. Clear the seed
        // range before each fact. The migration's BackfillMemberStatus
        // row is also cleared here, so the seed's MemberStatus is the
        // only one when the fact runs.
        Db.DutyRules.RemoveRange(Db.DutyRules);
        Db.ServiceDefinitionDuties.RemoveRange(Db.ServiceDefinitionDuties);
        Db.ServiceDefinitions.RemoveRange(Db.ServiceDefinitions);
        Db.ServiceTypes.RemoveRange(Db.ServiceTypes);
        Db.Admins.RemoveRange(Db.Admins);
        Db.Members.RemoveRange(Db.Members);
        Db.Duties.RemoveRange(Db.Duties);
        Db.Branches.RemoveRange(Db.Branches);
        Db.MemberStatuses.RemoveRange(Db.MemberStatuses);
        Db.Roles.RemoveRange(Db.Roles);
        await Db.SaveChangesAsync();
    }

    private async Task<(int branchId, int statusId, int roleId)> SeedBasicsAsync()
    {
        var role = new Role { Name = "Coordinator", IsDeleted = false };
        var defaultRole = new Role { Name = "Member", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var status = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };

        Db.Roles.Add(role);
        Db.Roles.Add(defaultRole);
        Db.Branches.Add(branch);
        Db.MemberStatuses.Add(status);
        await Db.SaveChangesAsync();

        return (branch.BranchId, status.MemberStatusId, role.RoleId);
    }

    [Fact]
    public async Task Role_Default_IsBlocked()
    {
        var (_, _, _) = await SeedBasicsAsync();

        var defaultRole = await Db.Roles.FirstAsync(r => r.IsDefault);
        var service = new ConfigurationCascadeService(Db);

        var result = await service.ApplyAsync(nameof(Role), defaultRole.RoleId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsBlocked.Should().BeTrue();
        result.Value.BlockedReason.Should().Contain("default Role");
    }

    [Fact]
    public async Task Role_NonDefault_FallsBackMembersAndSoftDeletesRules()
    {
        var (branchId, statusId, roleId) = await SeedBasicsAsync();
        var defaultRole = await Db.Roles.FirstAsync(r => r.IsDefault);

        var member = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branchId, RoleId = roleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(member);

        var rule = new DutyRule
        {
            DutyId = 1,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Coordinator",
            IsDeleted = false,
        };
        var duty = new Duty { Name = "Reading", IsDeleted = false };
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        rule.DutyId = duty.DutyId;
        Db.DutyRules.Add(rule);
        await Db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(Db);
        var result = await service.ApplyAsync(nameof(Role), roleId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsBlocked.Should().BeFalse();
        result.Value.ConsequencesText.Should().Contain("fell back");
        result.Value.ConsequencesText.Should().Contain("soft-deleted");

        // In-memory state was prepared.
        member.RoleId.Should().Be(defaultRole.RoleId);
        rule.IsDeleted.Should().BeTrue();
        rule.IsActive.Should().BeFalse();
    }

    [Fact]
    public async Task Role_NonDefault_WithoutDefaultRole_FailsConfigurationMissing()
    {
        var role = new Role { Name = "Only", IsDeleted = false };
        Db.Roles.Add(role);
        await Db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(Db);
        var result = await service.ApplyAsync(nameof(Role), role.RoleId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ConfigurationMissing);
    }

    [Fact]
    public async Task Duty_Referenced_IsBlocked()
    {
        var (_, _, _) = await SeedBasicsAsync();

        var duty = new Duty { Name = "Reading", IsDeleted = false };
        Db.Duties.Add(duty);

        var serviceType = new ServiceType { Name = "Regular" };
        Db.ServiceTypes.Add(serviceType);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = serviceType.ServiceTypeId, IsDeleted = false,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty.DutyId,
            RequiredSlotCount = 1,
            IsDeleted = false,
        });
        await Db.SaveChangesAsync();

        var cascade = new ConfigurationCascadeService(Db);
        var result = await cascade.ApplyAsync(nameof(Duty), duty.DutyId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsBlocked.Should().BeTrue();
        result.Value.BlockedReason.Should().Contain("referenced");
    }

    [Fact]
    public async Task Duty_Unreferenced_IsNotBlocked()
    {
        await SeedBasicsAsync();

        var duty = new Duty { Name = "Unused", IsDeleted = false };
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(Db);
        var result = await service.ApplyAsync(nameof(Duty), duty.DutyId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownEntityType_Fails()
    {
        await SeedBasicsAsync();

        var service = new ConfigurationCascadeService(Db);
        var result = await service.ApplyAsync("SomethingElse", 1);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
        result.Error.Message.Should().Contain("SomethingElse");
    }

    [Fact]
    public async Task DoesNotCommit()
    {
        var (branchId, statusId, roleId) = await SeedBasicsAsync();
        var defaultRole = await Db.Roles.FirstAsync(r => r.IsDefault);

        var member = new Member
        {
            Name = "A", Surname = "One",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branchId, RoleId = roleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(member);
        await Db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(Db);
        await service.ApplyAsync(nameof(Role), roleId);

        // Same context: no save yet, so a fresh context sees no change.
        await using var fresh = CreateContext();
        var persisted = await fresh.Members
            .FirstAsync(m => m.MemberId == member.MemberId);

        persisted.RoleId.Should().Be(roleId,
            "the cascade only prepares changes; it does not commit them");
    }
}
