using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageIdentifierHistory;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageIdentifierHistoryService.
/// </summary>
public class ManageIdentifierHistoryServiceTests : IntegrationTestBase
{
    private async Task<(int memberId, int adminId)> SeedAsync()
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
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        return (member.MemberId, admin.AdminId);
    }

    [Fact]
    public async Task Issue_WithInactiveAdmin_Fails()
    {
        // C6: the authorizing admin must resolve to an Admin row whose
        // IsActive = true.
        var (memberId, adminId) = await SeedAsync();

        var admin = await Db.Admins.FirstAsync(a => a.AdminId == adminId);
        admin.IsActive = false;
        await Db.SaveChangesAsync();

        var service = new ManageIdentifierHistoryService(Db);

        var result = await service.IssueAsync(
            memberId,
            "Card",
            "C-0001",
            new DateOnly(2026, 10, 14),
            adminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }

    // Matrix row 3.0:59eb29589b6a: A Member with a ReceiptNumber and no CardNumber, and the configured duration has elapsed | A CardNumber is set on the Member; the receipt's IdentifierHistory row is retired with `UnassignedDate` and a reason; the card's IdentifierHistory row is created
    [Fact]
    [Trait("MatrixRow", "3.0:59eb29589b6a")]
    public async Task ReceiptReplacedByCard_RetiresReceiptAndAssignsCard()
    {
        var (memberId, adminId) = await SeedAsync();

        // Issue the receipt first.
        var historyService = new ManageIdentifierHistoryService(Db);
        var receiptIssue = await historyService.IssueAsync(
            memberId, "Receipt", "R-001",
            new DateOnly(2024, 1, 1), adminId);
        receiptIssue.IsSuccess.Should().BeTrue();

        // Retire the receipt.
        var receiptEntryId = receiptIssue.Value!.EntryId;
        var retired = await historyService.RetireAsync(
            receiptEntryId, new DateOnly(2024, 4, 1), "duration elapsed");
        retired.IsSuccess.Should().BeTrue();

        // Issue the card.
        var cardIssue = await historyService.IssueAsync(
            memberId, "Card", "C-001",
            new DateOnly(2024, 4, 1), adminId);
        cardIssue.IsSuccess.Should().BeTrue();

        // Assert the history matches the row: one retired receipt,
        // one active card.
        Db.ChangeTracker.Clear();
        var history = await Db.IdentifierHistories.AsNoTracking()
            .Where(h => h.MemberId == memberId)
            .OrderBy(h => h.EntryId)
            .ToListAsync();

        history.Should().HaveCount(2);

        var receipt = history[0];
        receipt.Type.Should().Be("Receipt");
        receipt.UnassignedDate.Should().Be(new DateOnly(2024, 4, 1));

        var card = history[1];
        card.Type.Should().Be("Card");
        card.UnassignedDate.Should().BeNull();
    }
}
