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
public class RecordReadmissionServiceTests : IAsyncLifetime
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
            "TRUNCATE TABLE readmission, attendance_rule_scope, attendance_rule, " +
            "member_attribute_value, attribute_type, member_status, " +
            "member, branch, role, admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Fixture(int MemberId, int AdminId, int StatusId);

    private async Task<Fixture> SeedAsync(bool adminActive = true)
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

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
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
            IsActive = adminActive,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return new Fixture(member.MemberId, admin.AdminId, status.MemberStatusId);
    }

    [Fact]
    public async Task Record_WithValidMember_CreatesRow()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(_db);

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

        var persisted = await _db.Readmissions.AsNoTracking()
            .FirstAsync(r => r.ReadmissionId == result.Value.ReadmissionId);
        persisted.MemberId.Should().Be(f.MemberId);
    }

    [Fact]
    public async Task Record_WithMissingMember_Fails()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(_db);

        var result = await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = 9999,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.NotFound);

        var count = await _db.Readmissions.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_WithInactiveAdmin_Fails()
    {
        var f = await SeedAsync(adminActive: false);
        var service = new RecordReadmissionService(_db);

        var result = await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);

        var count = await _db.Readmissions.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_DoesNotModifyMemberStatus()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(_db);

        var result = await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        result.IsSuccess.Should().BeTrue();

        _db.ChangeTracker.Clear();
        var member = await _db.Members.AsNoTracking()
            .FirstAsync(m => m.MemberId == f.MemberId);
        member.MemberStatusId.Should().Be(f.StatusId);
    }

    [Fact]
    public async Task Record_DoesNotWriteAuditLog()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(_db);

        var before = await _db.ConfigurationAuditLogs.CountAsync();

        await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        var after = await _db.ConfigurationAuditLogs.CountAsync();
        after.Should().Be(before);
    }

    [Fact]
    public async Task Record_IncreasesReadmissionCount()
    {
        var f = await SeedAsync();
        var service = new RecordReadmissionService(_db);

        await service.RecordAsync(new RecordReadmissionCommand
        {
            MemberId = f.MemberId,
            ReadmissionDate = new DateOnly(2026, 10, 1),
            ActingAdminId = f.AdminId,
        });

        var count = await _db.Readmissions
            .CountAsync(r => r.MemberId == f.MemberId);
        count.Should().Be(1);
    }
}
