using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageProgramItem;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D9_EventProgram;
using MknMs.Domain.D14_ConfigurationAudit;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageProgramItemService.SoftDeleteAsync and the
/// ProgramItem cascade.
/// </summary>
public class ManageProgramItemServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

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
            "notification_subscription, event_duty, program_item, program, " +
            "member, member_status, branch, role, duty, event, admin, " +
            "permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private sealed record Fixture(int ItemId, int AdminId);

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
        var duty = new Duty { Name = "MC duty", IsDeleted = false };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

        _db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "ProgramItem",
            RequiresApprovalForDelete = false,
            RequiresApprovalForDeactivate = false,
            RequiresReasonForDelete = true,
            RequiresReasonForDeactivate = false,
            IsActive = true,
            IsDeleted = false,
        });
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

        var evt = new Event
        {
            Name = "Conf",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 2),
            Type = "Conference",
            HostBranchId = branch.BranchId,
            IsDeleted = false,
        };
        _db.Events.Add(evt);
        await _db.SaveChangesAsync();

        var program = new MknMs.Domain.D9_EventProgram.Program
        {
            EventId = evt.EventId,
            Title = "Main",
            IsDeleted = false,
        };
        _db.Programs.Add(program);
        await _db.SaveChangesAsync();

        var item = new ProgramItem
        {
            ProgramId = program.ProgramId,
            SequenceOrder = 1,
            Title = "Opening",
            ScheduledStart = Now.AddDays(15),
            ScheduledEnd = Now.AddDays(15).AddHours(1),
            IsDeleted = false,
        };
        _db.ProgramItems.Add(item);
        await _db.SaveChangesAsync();

        return new Fixture(item.ItemId, admin.AdminId);
    }

    private ManageProgramItemService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_RetainsItem()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ItemId, f.AdminId, f.AdminId, true, "cancelled");

        result.IsSuccess.Should().BeTrue();



        var item = await _db.ProgramItems
            .AsNoTracking()
            .FirstAsync(i => i.ItemId == f.ItemId);
        item.IsDeleted.Should().BeTrue();

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
        audit.ConsequencesPreviewed.Should().Contain("No dependent changes apply");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ItemId, f.AdminId, f.AdminId,
            consequencesAcknowledged: false, "attempt");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ValidationFailed);
    }

    [Fact]
    public async Task SoftDelete_WithoutReason_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ItemId, f.AdminId, f.AdminId, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task SoftDelete_AlreadyDeleted_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(f.ItemId, f.AdminId, f.AdminId, true, "one");
        var second = await service.SoftDeleteAsync(f.ItemId, f.AdminId, f.AdminId, true, "two");

        second.IsSuccess.Should().BeFalse();
        second.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyInactive);
    }

    [Fact]
    public async Task Create_WithInactiveAdmin_Fails()
    {
        // C6: the acting admin must resolve to an Admin row whose
        // IsActive = true.
        var f = await SeedAsync();
        var service = MakeService();

        var admin = await _db.Admins.FirstAsync(a => a.AdminId == f.AdminId);
        admin.IsActive = false;
        await _db.SaveChangesAsync();

        var program = await _db.Programs.FirstAsync();

        var result = await service.CreateAsync(
            program.ProgramId,
            1,
            "New item",
            new DateTimeOffset(2026, 11, 1, 9, 0, 0, TimeSpan.Zero),
            new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero),
            serviceDefId: null,
            actingAdminId: f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.ReferencedEntityInactive);
    }
}
