using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageMemberRecord;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for SetMemberAttributeService — the writer for
/// MemberAttributeValue.
/// </summary>
public class SetMemberAttributeServiceTests : IntegrationTestBase
{
    private sealed record Fixture(int MemberId, int AdminId, int AttrId);

    private async Task<Fixture> SeedAsync(bool attrSoftDeleted = false)
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var attr = new AttributeType
        {
            Name = "Purity", IsActive = !attrSoftDeleted, IsDeleted = attrSoftDeleted,
        };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.AttributeTypes.Add(attr);
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
            IsActive = true,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        return new Fixture(member.MemberId, admin.AdminId, attr.AttributeTypeId);
    }

    [Fact]
    public async Task Set_NewPair_InsertsRow()
    {
        var f = await SeedAsync();
        var service = new SetMemberAttributeService(Db);

        var result = await service.SetAsync(new SetMemberAttributeCommand
        {
            MemberId = f.MemberId,
            AttributeTypeId = f.AttrId,
            Value = "Pure",
            RecordedDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.Value.Should().Be("Pure");
        result.Value.RecordedDate.Should().Be(new DateOnly(2026, 10, 1));
        result.Value.RecordedBy.Should().Be(f.AdminId);

        var count = await Db.MemberAttributeValues.CountAsync();
        count.Should().Be(1);
    }

    // Matrix row 3.0:5d52300b1bae: A Member with a MemberAttributeValue | The existing value is replaced; uniqueness preserved
    [Fact]
    [Trait("MatrixRow", "3.0:5d52300b1bae")]
    public async Task Set_ExistingPair_ReplacesValue()
    {
        var f = await SeedAsync();
        var service = new SetMemberAttributeService(Db);

        var first = await service.SetAsync(new SetMemberAttributeCommand
        {
            MemberId = f.MemberId,
            AttributeTypeId = f.AttrId,
            Value = "Pure",
            RecordedDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });
        first.IsSuccess.Should().BeTrue();
        var firstId = first.Value!.MemberAttributeValueId;

        var second = await service.SetAsync(new SetMemberAttributeCommand
        {
            MemberId = f.MemberId,
            AttributeTypeId = f.AttrId,
            Value = "Impure",
            RecordedDate = new DateOnly(2026, 10, 15),
            ActingAdminId = f.AdminId,
        });

        second.IsSuccess.Should().BeTrue();
        second.Value!.MemberAttributeValueId.Should().Be(firstId);

        var count = await Db.MemberAttributeValues.CountAsync();
        count.Should().Be(1);

        var persisted = await Db.MemberAttributeValues.AsNoTracking()
            .FirstAsync();
        persisted.Value.Should().Be("Impure");
        persisted.RecordedDate.Should().Be(new DateOnly(2026, 10, 15));
    }

    [Fact]
    public async Task Set_WithMissingMember_Fails()
    {
        var f = await SeedAsync();
        var service = new SetMemberAttributeService(Db);

        var result = await service.SetAsync(new SetMemberAttributeCommand
        {
            MemberId = 9999,
            AttributeTypeId = f.AttrId,
            Value = "Pure",
            RecordedDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.NotFound);
    }

    [Fact]
    public async Task Set_WithSoftDeletedAttributeType_Fails()
    {
        var f = await SeedAsync(attrSoftDeleted: true);
        var service = new SetMemberAttributeService(Db);

        var result = await service.SetAsync(new SetMemberAttributeCommand
        {
            MemberId = f.MemberId,
            AttributeTypeId = f.AttrId,
            Value = "Pure",
            RecordedDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task Set_WithInactiveAdmin_Fails()
    {
        var f = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new SetMemberAttributeService(Db);

        var result = await service.SetAsync(new SetMemberAttributeCommand
        {
            MemberId = f.MemberId,
            AttributeTypeId = f.AttrId,
            Value = "Pure",
            RecordedDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    [Fact]
    public async Task Set_WithBlankValue_Fails()
    {
        var f = await SeedAsync();
        var service = new SetMemberAttributeService(Db);

        var result = await service.SetAsync(new SetMemberAttributeCommand
        {
            MemberId = f.MemberId,
            AttributeTypeId = f.AttrId,
            Value = "   ",
            RecordedDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }
}
