using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageAttributeType;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageAttributeTypeService.SoftDeleteAsync and the
/// AttributeType cascade.
/// </summary>
public class ManageAttributeTypeServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 22, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, member_attribute_value, attribute_type, " +
            "duty_rule, member, member_status, branch, role, duty, admin, " +
            "permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Fixture(
        int attrId,
        int otherAttrId,
        int adminId,
        int matchingRuleId,
        int substringRuleId);

    private async Task<Fixture> SeedAsync()
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
        var attr = new AttributeType { Name = "Purity", IsDeleted = false };
        var otherAttr = new AttributeType { Name = "Pur", IsDeleted = false };
        var duty = new Duty { Name = "Reading", IsDeleted = false };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.AttributeTypes.Add(attr);
        _db.AttributeTypes.Add(otherAttr);
        _db.Duties.Add(duty);
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

        // Attribute value for the deleted attribute.
        _db.MemberAttributeValues.Add(new MemberAttributeValue
        {
            MemberId = member.MemberId,
            AttributeTypeId = attr.AttributeTypeId,
            Value = "Pure",
            RecordedDate = new DateOnly(2026, 1, 1),
            RecordedBy = admin.AdminId,
        });

        // Attribute value for the other attribute (should be kept).
        _db.MemberAttributeValues.Add(new MemberAttributeValue
        {
            MemberId = member.MemberId,
            AttributeTypeId = otherAttr.AttributeTypeId,
            Value = "SomeValue",
            RecordedDate = new DateOnly(2026, 1, 1),
            RecordedBy = admin.AdminId,
        });

        // A DutyRule whose MemberAttribute criterion names "Purity".
        var matchingRule = new DutyRule
        {
            DutyId = duty.DutyId,
            TierOrder = 1,
            CriteriaType = "MemberAttribute",
            CriteriaValue = "Purity=Pure",
            IsDeleted = false,
        };
        // A DutyRule whose value starts with "Pur" — substring-wise
        // inside "Purity", but a different attribute name.
        var substringRule = new DutyRule
        {
            DutyId = duty.DutyId,
            TierOrder = 2,
            CriteriaType = "MemberAttribute",
            CriteriaValue = "Pur=SomeValue",
            IsDeleted = false,
        };
        _db.DutyRules.Add(matchingRule);
        _db.DutyRules.Add(substringRule);
        await _db.SaveChangesAsync();

        return new Fixture(
            attr.AttributeTypeId,
            otherAttr.AttributeTypeId,
            admin.AdminId,
            matchingRule.RuleId,
            substringRule.RuleId);
    }

    private ManageAttributeTypeService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_RemovesValuesForTheAttribute()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.attrId, f.adminId, f.adminId, true, "retired");

        result.IsSuccess.Should().BeTrue();

        var remaining = await _db.MemberAttributeValues
            .Where(v => v.AttributeTypeId == f.attrId)
            .CountAsync();
        remaining.Should().Be(0);

        // The other attribute's value is preserved.
        var otherRemaining = await _db.MemberAttributeValues
            .Where(v => v.AttributeTypeId == f.otherAttrId)
            .CountAsync();
        otherRemaining.Should().Be(1);
    }

    [Fact]
    public async Task SoftDelete_SoftDeletesDutyRulesNamingTheAttribute()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            f.attrId, f.adminId, f.adminId, true, "retired");

        var matching = await _db.DutyRules.FirstAsync(r => r.RuleId == f.matchingRuleId);
        matching.IsDeleted.Should().BeTrue();
        matching.IsActive.Should().BeFalse();

        // The rule naming "Pur" is not touched.
        var untouched = await _db.DutyRules.FirstAsync(r => r.RuleId == f.substringRuleId);
        untouched.IsDeleted.Should().BeFalse();
        untouched.IsActive.Should().BeTrue();
    }

    [Fact]
    public async Task SoftDelete_WritesAuditWithCascadeText()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(
            f.attrId, f.adminId, f.adminId, true, "retired");

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
        audit.EntityType.Should().Be("AttributeType");
        audit.ConsequencesPreviewed.Should().Contain("removed");
        audit.ConsequencesPreviewed.Should().Contain("soft-deleted");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.attrId, f.adminId, f.adminId, consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }
}
