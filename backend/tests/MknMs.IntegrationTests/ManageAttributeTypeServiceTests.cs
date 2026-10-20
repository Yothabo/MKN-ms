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
using MknMs.Domain.D14_ConfigurationAudit;


namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageAttributeTypeService.SoftDeleteAsync and the
/// AttributeType cascade.
/// </summary>
public class ManageAttributeTypeServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 22, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

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
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var attr = new AttributeType { Name = "Purity", IsDeleted = false };
        var otherAttr = new AttributeType { Name = "Pur", IsDeleted = false };
        var duty = new Duty { Name = "Reading", IsDeleted = false };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.AttributeTypes.Add(attr);
        Db.AttributeTypes.Add(otherAttr);
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "AttributeType",
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

        // Attribute value for the deleted attribute.
        Db.MemberAttributeValues.Add(new MemberAttributeValue
        {
            MemberId = member.MemberId,
            AttributeTypeId = attr.AttributeTypeId,
            Value = "Pure",
            RecordedDate = new DateOnly(2026, 1, 1),
            RecordedBy = admin.AdminId,
        });

        // Attribute value for the other attribute (should be kept).
        Db.MemberAttributeValues.Add(new MemberAttributeValue
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
        Db.DutyRules.Add(matchingRule);
        Db.DutyRules.Add(substringRule);
        await Db.SaveChangesAsync();

        return new Fixture(
            attr.AttributeTypeId,
            otherAttr.AttributeTypeId,
            admin.AdminId,
            matchingRule.RuleId,
            substringRule.RuleId);
    }

    private ManageAttributeTypeService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_RemovesValuesForTheAttribute()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.attrId, f.adminId, f.adminId, true, "retired");

        result.IsSuccess.Should().BeTrue();

        var remaining = await Db.MemberAttributeValues
            .Where(v => v.AttributeTypeId == f.attrId)
            .CountAsync();
        remaining.Should().Be(0);

        // The other attribute's value is preserved.
        var otherRemaining = await Db.MemberAttributeValues
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

        var matching = await Db.DutyRules.FirstAsync(r => r.RuleId == f.matchingRuleId);
        matching.IsDeleted.Should().BeTrue();
        matching.IsActive.Should().BeFalse();

        // The rule naming "Pur" is not touched.
        var untouched = await Db.DutyRules.FirstAsync(r => r.RuleId == f.substringRuleId);
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

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
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

    [Fact]
    public async Task List_ReturnsNonDeletedOrdered()
    {
        var f = await SeedAsync();

        var service = MakeService();
        var list = await service.ListAsync();

        list.Should().NotBeEmpty();
        list.Should().OnlyContain(a => !a.IsDeleted);
    }

    [Fact]
    public async Task Create_WithNameAndDescription_WritesRow()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync(
            "Baptised", "Whether the member is baptised.", f.adminId);

        result.IsSuccess.Should().BeTrue();
        result.Value!.Name.Should().Be("Baptised");
        result.Value.Description.Should().Be("Whether the member is baptised.");
        result.Value.IsActive.Should().BeTrue();
        result.Value.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Create_WithBlankName_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync("   ", null, f.adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        var f = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == f.adminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = MakeService();
        var result = await service.CreateAsync("Tag", null, f.adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
}
