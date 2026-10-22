using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Configuration.ManageMemberRecord;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageMemberRecordService.CreateAsync.
/// </summary>
public class ManageMemberRecordServiceTests : IntegrationTestBase
{
    private async Task<(int branchId, int roleId, int statusId)> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        await Db.SaveChangesAsync();

        return (branch.BranchId, role.RoleId, statusId);
    }

    private ManageMemberRecordService MakeService() => new(Db);

    // Matrix row 3.0:29b2db21dfe6: A new Member with all required fields | The row exists; `MemberStatusID` references the configured default status
    [Fact]
    [Trait("MatrixRow", "3.0:29b2db21dfe6")]
    public async Task CreateMember_WithAllFields_PersistsAndDefaults()
    {
        var (branchId, roleId, statusId) = await SeedAsync();
        var service = MakeService();

        var result = await service.CreateAsync(new CreateMemberRequest
        {
            Name = "Test", Surname = "Member",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-0", Email = null,
            BranchId = branchId, RoleId = roleId,
            MemberStatusId = statusId,
        });

        result.IsSuccess.Should().BeTrue($"error: {result.Error?.Message}");

        Db.ChangeTracker.Clear();
        var member = await Db.Members.AsNoTracking()
            .FirstAsync(m => m.Name == "Test" && m.Surname == "Member");

        member.MemberStatusId.Should().Be(statusId);
    }
}
