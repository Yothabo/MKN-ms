using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageProgram;
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
/// Tests for ManageProgramService.SoftDeleteAsync and the Program
/// cascade.
/// </summary>
public class ManageProgramServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 14, 8, 0, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private sealed record Fixture(int ProgramId, int AdminId, int ItemId);

    private async Task<Fixture> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var duty = new Duty { Name = "MC duty", IsDeleted = false };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Program",
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

        var evt = new Event
        {
            Name = "Conf",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 2),
            Type = "Conference",
            HostBranchId = branch.BranchId,
            IsDeleted = false,
        };
        Db.Events.Add(evt);
        await Db.SaveChangesAsync();

        var program = new MknMs.Domain.D9_EventProgram.Program
        {
            EventId = evt.EventId,
            Title = "Main",
            IsDeleted = false,
        };
        Db.Programs.Add(program);
        await Db.SaveChangesAsync();

        var item = new ProgramItem
        {
            ProgramId = program.ProgramId,
            SequenceOrder = 1,
            Title = "Opening",
            ScheduledStart = Now.AddDays(15),
            ScheduledEnd = Now.AddDays(15).AddHours(1),
            IsDeleted = false,
        };
        Db.ProgramItems.Add(item);
        await Db.SaveChangesAsync();

        return new Fixture(program.ProgramId, admin.AdminId, item.ItemId);
    }

    private ManageProgramService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_RemovesItems_RetainsProgram()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ProgramId, f.AdminId, f.AdminId, true, "replaced");

        result.IsSuccess.Should().BeTrue();

        (await Db.ProgramItems.CountAsync()).Should().Be(0);

        var program = await Db.Programs
            .AsNoTracking()
            .FirstAsync(p => p.ProgramId == f.ProgramId);
        program.IsDeleted.Should().BeTrue();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.Action.Should().Be(ConfigurationLifecycleActions.SoftDelete);
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.ProgramId, f.AdminId, f.AdminId,
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
            f.ProgramId, f.AdminId, f.AdminId, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }

    [Fact]
    public async Task SoftDelete_AlreadyDeleted_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(f.ProgramId, f.AdminId, f.AdminId, true, "one");
        var second = await service.SoftDeleteAsync(f.ProgramId, f.AdminId, f.AdminId, true, "two");

        second.IsSuccess.Should().BeFalse();
        second.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyInactive);
    }

    // Matrix row 8.0:75ebf75fea80: A second Program for the same Event | The operation is rejected with `Conflict`; one Program per Event
    [Fact]
    [Trait("MatrixRow", "8.0:75ebf75fea80")]
    public async Task Create_SecondProgramForSameEvent_Conflict()
    {
        var f = await SeedAsync();
        var service = MakeService();

        // The fixture already created a Program for this Event.
        var eventId = await Db.Programs
            .Where(pr => pr.ProgramId == f.ProgramId)
            .Select(pr => pr.EventId)
            .FirstAsync();

        var second = await service.CreateAsync(eventId, "Second");

        second.IsSuccess.Should().BeFalse(
            "one Program per Event is a schema-level invariant");
        second.Error!.Code.Should().Be(ConfigurationErrorCodes.DuplicateName);

        var count = await Db.Programs.CountAsync(pr => pr.EventId == eventId);
        count.Should().Be(1);
    }
}
