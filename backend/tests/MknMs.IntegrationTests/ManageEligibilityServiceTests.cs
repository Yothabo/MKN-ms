using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageEligibility;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageEligibilityService.
/// </summary>
public class ManageEligibilityServiceTests : IntegrationTestBase
{
    private async Task<(int memberId, int dutyId, int adminId)> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var duty = new Duty { Name = "Reading duty", IsDeleted = false };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.Duties.Add(duty);
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

        return (member.MemberId, duty.DutyId, admin.AdminId);
    }

    [Fact]
    public async Task Grant_WithInactiveAdmin_Fails()
    {
        // C6: the granting admin must resolve to an Admin row whose
        // IsActive = true.
        var (memberId, dutyId, adminId) = await SeedAsync();

        var admin = await Db.Admins.FirstAsync(a => a.AdminId == adminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new ManageEligibilityService(Db);

        var result = await service.GrantAsync(
            memberId,
            dutyId,
            new DateOnly(2026, 10, 14),
            adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
}
