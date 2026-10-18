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
public class SetMemberAttributeServiceTests : IAsyncLifetime
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
            "TRUNCATE TABLE member_attribute_value, attribute_type, " +
            "member, member_status, branch, role, admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Fixture(int MemberId, int AdminId, int AttrId);

    private async Task<Fixture> SeedAsync(bool attrSoftDeleted = false)
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
        var attr = new AttributeType
        {
            Name = "Purity", IsActive = !attrSoftDeleted, IsDeleted = attrSoftDeleted,
        };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.AttributeTypes.Add(attr);
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
            IsActive = true,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return new Fixture(member.MemberId, admin.AdminId, attr.AttributeTypeId);
    }

    [Fact]
    public async Task Set_NewPair_InsertsRow()
    {
        var f = await SeedAsync();
        var service = new SetMemberAttributeService(_db);

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

        var count = await _db.MemberAttributeValues.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Set_ExistingPair_ReplacesValue()
    {
        var f = await SeedAsync();
        var service = new SetMemberAttributeService(_db);

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

        var count = await _db.MemberAttributeValues.CountAsync();
        count.Should().Be(1);

        var persisted = await _db.MemberAttributeValues.AsNoTracking()
            .FirstAsync();
        persisted.Value.Should().Be("Impure");
        persisted.RecordedDate.Should().Be(new DateOnly(2026, 10, 15));
    }

    [Fact]
    public async Task Set_WithMissingMember_Fails()
    {
        var f = await SeedAsync();
        var service = new SetMemberAttributeService(_db);

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
        var service = new SetMemberAttributeService(_db);

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
        var admin = await _db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await _db.SaveChangesAsync();

        var service = new SetMemberAttributeService(_db);

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
        var service = new SetMemberAttributeService(_db);

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
