using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageMemberRecord;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for SetIdentifierAsync (§15.8.41).
/// </summary>
public class SetMemberIdentifierServiceTests : IntegrationTestBase
{
    private async Task<(int MemberId, int AdminId)> SeedAsync()
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

        return (member.MemberId, admin.AdminId);
    }

    [Fact]
    public async Task SetCard_SetsCardNumber_LeavesReceiptUnchanged()
    {
        var (memberId, adminId) = await SeedAsync();
        var service = new ManageMemberRecordService(Db);

        var result = await service.SetIdentifierAsync(
            memberId, "C-001", null, adminId);

        result.IsSuccess.Should().BeTrue();
        Db.ChangeTracker.Clear();
        var member = await Db.Members.AsNoTracking()
            .FirstAsync(m => m.MemberId == memberId);
        member.CardNumber.Should().Be("C-001");
        member.ReceiptNumber.Should().BeNull();
    }

    [Fact]
    public async Task SetReceipt_SetsReceiptNumber_LeavesCardUnchanged()
    {
        var (memberId, adminId) = await SeedAsync();
        var service = new ManageMemberRecordService(Db);

        var result = await service.SetIdentifierAsync(
            memberId, null, "R-001", adminId);

        result.IsSuccess.Should().BeTrue();
        Db.ChangeTracker.Clear();
        var member = await Db.Members.AsNoTracking()
            .FirstAsync(m => m.MemberId == memberId);
        member.ReceiptNumber.Should().Be("R-001");
        member.CardNumber.Should().BeNull();
    }

    [Fact]
    public async Task SetBoth_SetsBoth()
    {
        var (memberId, adminId) = await SeedAsync();
        var service = new ManageMemberRecordService(Db);

        var result = await service.SetIdentifierAsync(
            memberId, "C-002", "R-002", adminId);

        result.IsSuccess.Should().BeTrue();
        Db.ChangeTracker.Clear();
        var member = await Db.Members.AsNoTracking()
            .FirstAsync(m => m.MemberId == memberId);
        member.CardNumber.Should().Be("C-002");
        member.ReceiptNumber.Should().Be("R-002");
    }

    [Fact]
    public async Task SetNeither_Fails()
    {
        var (memberId, adminId) = await SeedAsync();
        var service = new ManageMemberRecordService(Db);

        var result = await service.SetIdentifierAsync(
            memberId, null, null, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task SetEmptyString_ClearsColumn()
    {
        var (memberId, adminId) = await SeedAsync();
        var service = new ManageMemberRecordService(Db);

        await service.SetIdentifierAsync(memberId, "C-003", null, adminId);
        var result = await service.SetIdentifierAsync(memberId, "", null, adminId);

        result.IsSuccess.Should().BeTrue();
        Db.ChangeTracker.Clear();
        var member = await Db.Members.AsNoTracking()
            .FirstAsync(m => m.MemberId == memberId);
        member.CardNumber.Should().BeNull();
    }

    [Fact]
    public async Task SetWithInactiveAdmin_Fails()
    {
        var (memberId, adminId) = await SeedAsync();
        var admin = await Db.Admins.FirstAsync(a => a.AdminId == adminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new ManageMemberRecordService(Db);
        var result = await service.SetIdentifierAsync(
            memberId, "C-004", null, adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
}
