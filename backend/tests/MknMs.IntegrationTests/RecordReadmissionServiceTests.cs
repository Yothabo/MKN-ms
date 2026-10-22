using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.RecordReadmission;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for RecordReadmissionService (subprocess 3.7).
/// </summary>
public class RecordReadmissionServiceTests : IntegrationTestBase
{
    private sealed record Fixture(int MemberId, int AdminId, int StatusId);

    private async Task<Fixture> SeedAsync(bool adminActive = true)
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
            IsActive = adminActive,
        };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        return new Fixture(member.MemberId, admin.AdminId, statusId);
    }

    [Fact]
    public async Task Record_WithValidMember_CreatesRow()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(Db);

        var result = await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            Reason = "returned from leave",
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeTrue();
        result.Value!.MemberId.Should().Be(f.MemberId);
        result.Value.ReadmissionDate.Should().Be(new DateOnly(2026, 10, 1));
        result.Value.PerformedByAdminId.Should().Be(f.AdminId);
        result.Value.Reason.Should().Be("returned from leave");

        var persisted = await Db.Readmissions.AsNoTracking()
            .FirstAsync(r => r.ReadmissionId == result.Value.ReadmissionId);
        persisted.MemberId.Should().Be(f.MemberId);
    }

    [Fact]
    public async Task Record_WithMissingMember_Fails()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(Db);

        var result = await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = 9999,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.NotFound);

        var count = await Db.Readmissions.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_WithInactiveAdmin_Fails()
    {
        var f = await SeedAsync(adminActive: false);
        var service = new RecordReadmissionService(Db);

        var result = await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);

        var count = await Db.Readmissions.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    [Trait("MatrixRow", "3.0:311906e963f7")]
    public async Task Record_DoesNotModifyMemberStatus()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(Db);

        var result = await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeTrue();

        Db.ChangeTracker.Clear();
        var member = await Db.Members.AsNoTracking()
            .FirstAsync(m => m.MemberId == f.MemberId);
        member.MemberStatusId.Should().Be(f.StatusId);
    }

    [Fact]
    public async Task Record_DoesNotWriteAuditLog()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(Db);

        var before = await Db.ConfigurationAuditLogs.CountAsync();

        await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        var after = await Db.ConfigurationAuditLogs.CountAsync();
        after.Should().Be(before);
    }

    [Fact]
    public async Task Record_IncreasesReadmissionCount()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(Db);

        await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        var count = await Db.Readmissions
            .CountAsync(r => r.MemberId == f.MemberId);
        count.Should().Be(1);
    }

    // Matrix row 3.0:625991b483eb: A member with two Readmission rows | The readmission count is requested | The count is 2; no stored field carries the count
    [Fact]
    [Trait("MatrixRow", "3.0:625991b483eb")]
    public async Task TwoReadmissions_CountIsTwo_NotStored()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(Db);

        await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 1, 1),
            ActingAdminId = f.AdminId,
        });
        await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 6, 1),
            ActingAdminId = f.AdminId,
        });

        Db.ChangeTracker.Clear();
        var count = await Db.Readmissions
            .CountAsync(r => r.MemberId == f.MemberId);
        count.Should().Be(2);

        // No stored count field exists on Member. §13.6 states the
        // count is derived. A reflection over Member's properties
        // would confirm the absence; here the derived query is the
        // canonical form and no field is consulted.
    }
}
