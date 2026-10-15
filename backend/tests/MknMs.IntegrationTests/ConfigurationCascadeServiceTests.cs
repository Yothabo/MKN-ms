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
public class ConfigurationCascadeServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private MknDbContext _db = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE configuration_audit_log, entity_deletion_policy, " +
            "notification_subscription, roster_assignment, event_duty, " +
            "program_item, program, event_branch, event, " +
            "service_definition_duty, service_schedule, " +
            "duty_rule, member, member_status, branch, role, duty, " +
            "admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
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

        _db.Roles.Add(role);
        _db.Roles.Add(defaultRole);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        await _db.SaveChangesAsync();

        return (branch.BranchId, status.MemberStatusId, role.RoleId);
    }

    [Fact]
    public async Task Role_Default_IsBlocked()
    {
        var (_, _, _) = await SeedBasicsAsync();

        var defaultRole = await _db.Roles.FirstAsync(r => r.IsDefault);
        var service = new ConfigurationCascadeService(_db);

        var result = await service.ApplyAsync(nameof(Role), defaultRole.RoleId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsBlocked.Should().BeTrue();
        result.Value.BlockedReason.Should().Contain("default Role");
    }

    [Fact]
    public async Task Role_NonDefault_FallsBackMembersAndSoftDeletesRules()
    {
        var (branchId, statusId, roleId) = await SeedBasicsAsync();
        var defaultRole = await _db.Roles.FirstAsync(r => r.IsDefault);

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
        _db.Members.Add(member);

        var rule = new DutyRule
        {
            DutyId = 1,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Coordinator",
            IsDeleted = false,
        };
        var duty = new Duty { Name = "Reading", IsDeleted = false };
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

        rule.DutyId = duty.DutyId;
        _db.DutyRules.Add(rule);
        await _db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(_db);
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
        _db.Roles.Add(role);
        await _db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(_db);
        var result = await service.ApplyAsync(nameof(Role), role.RoleId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ConfigurationMissing);
    }

    [Fact]
    public async Task Duty_Referenced_IsBlocked()
    {
        var (_, _, _) = await SeedBasicsAsync();

        var duty = new Duty { Name = "Reading", IsDeleted = false };
        _db.Duties.Add(duty);

        var serviceType = new ServiceType { Name = "Regular" };
        _db.ServiceTypes.Add(serviceType);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "S", ServiceTypeId = serviceType.ServiceTypeId, IsDeleted = false,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty.DutyId,
            RequiredSlotCount = 1,
            IsDeleted = false,
        });
        await _db.SaveChangesAsync();

        var cascade = new ConfigurationCascadeService(_db);
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
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(_db);
        var result = await service.ApplyAsync(nameof(Duty), duty.DutyId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.IsBlocked.Should().BeFalse();
    }

    [Fact]
    public async Task UnknownEntityType_Fails()
    {
        await SeedBasicsAsync();

        var service = new ConfigurationCascadeService(_db);
        var result = await service.ApplyAsync("SomethingElse", 1);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
        result.Error.Message.Should().Contain("SomethingElse");
    }

    [Fact]
    public async Task DoesNotCommit()
    {
        var (branchId, statusId, roleId) = await SeedBasicsAsync();
        var defaultRole = await _db.Roles.FirstAsync(r => r.IsDefault);

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
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var service = new ConfigurationCascadeService(_db);
        await service.ApplyAsync(nameof(Role), roleId);

        // Same context: no save yet, so a fresh context sees no change.
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;
        await using var fresh = new MknDbContext(options);
        var persisted = await fresh.Members
            .FirstAsync(m => m.MemberId == member.MemberId);

        persisted.RoleId.Should().Be(roleId,
            "the cascade only prepares changes; it does not commit them");
    }
}
