using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageEvent;
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
/// Tests for ManageEventService.SoftDeleteAsync and the Event
/// cascade.
/// </summary>
public class ManageEventServiceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 22, 30, 0, TimeSpan.Zero);

    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    private sealed record Fixture(
        int EventId,
        int AdminId,
        int ProgramId,
        int ItemId,
        int EventDutyId);

    private async Task<Fixture> SeedAsync()
    {
        var role = new Role { Name = "R", IsDefault = true, IsDeleted = false };
        var branch = new Branch { Name = "B", Location = "L", IsDeleted = false };
        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var duty = new Duty { Name = "MC duty", IsActive = true, IsDeleted = false };

        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        Db.PermissionTiers.Add(tier);
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        Db.EntityDeletionPolicies.Add(new EntityDeletionPolicy
        {
            EntityType = "Event",
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
            Name = "Conference",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 3),
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

        var eventDuty = new EventDuty
        {
            EventId = evt.EventId,
            DutyId = duty.DutyId,
            RequiredSlotCount = 1,
            IsDeleted = false,
        };
        Db.EventDuties.Add(eventDuty);
        await Db.SaveChangesAsync();

        return new Fixture(
            evt.EventId,
            admin.AdminId,
            program.ProgramId,
            item.ItemId,
            eventDuty.EventDutyId);
    }

    private ManageEventService MakeService() =>
        new(Db,
            new ConfigurationLifecycleService(Db, _clock),
            new ConfigurationCascadeService(Db));

    [Fact]
    public async Task SoftDelete_RemovesProgramItemEventDutyCascade()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.EventId, f.AdminId, f.AdminId, true, "retired");

        result.IsSuccess.Should().BeTrue();

        (await Db.EventDuties.CountAsync()).Should().Be(0);
        (await Db.ProgramItems.CountAsync()).Should().Be(0);
        (await Db.Programs.CountAsync()).Should().Be(0);

        var evt = await Db.Events
            .AsNoTracking()
            .FirstAsync(e => e.EventId == f.EventId);
        evt.IsDeleted.Should().BeTrue();
        evt.IsActive.Should().BeFalse();

        var audit = await Db.ConfigurationAuditLogs.SingleAsync();
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_PreservesEventSourcedOccurrences()
    {
        var f = await SeedAsync();

        var st = new ServiceType { Name = "Regular" };
        Db.ServiceTypes.Add(st);
        await Db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = null,
            EventId = f.EventId,
            Date = new DateOnly(2026, 11, 1),
            ServiceTypeId = st.ServiceTypeId,
            StartTime = new TimeOnly(9, 0),
            FillStatusId = null,
            GeneratedBy = "Administrator",
            CreatedBy = f.AdminId,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        var service = MakeService();
        await service.SoftDeleteAsync(
            f.EventId, f.AdminId, f.AdminId, true, "retired");

        var stillThere = await Db.ServiceOccurrences
            .AnyAsync(o => o.EventId == f.EventId);
        stillThere.Should().BeTrue(
            "§9.7 preserves event-sourced Service Occurrence rows");
    }

    [Fact]
    public async Task SoftDelete_WithoutAcknowledgement_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.EventId, f.AdminId, f.AdminId,
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
            f.EventId, f.AdminId, f.AdminId, true, "");

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.InvalidValue);
    }
    [Fact]
    public async Task List_ExcludesSoftDeleted()
    {
        var f = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(f.EventId, f.AdminId, f.AdminId, true, "test");

        var events = await service.ListAsync();
        events.Should().NotContain(e => e.EventId == f.EventId);
    }

    [Fact]
    public async Task List_IncludeInactive_ExcludesSoftDeleted()
    {
        var f = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(f.EventId, f.AdminId, f.AdminId, true, "test");

        var events = await service.ListAsync(includeInactive: true);
        events.Should().NotContain(e => e.EventId == f.EventId);
    }

    [Fact]
    public async Task Get_SoftDeleted_ReturnsNull()
    {
        var f = await SeedAsync();
        var service = MakeService();
        await service.SoftDeleteAsync(f.EventId, f.AdminId, f.AdminId, true, "test");

        var evt = await service.GetAsync(f.EventId);
        evt.Should().BeNull();
    }

    [Fact]
    public async Task Reactivate_DeactivatedEvent_Succeeds()
    {
        // §9.7: deactivation is reversible.
        var f = await SeedAsync();
        var service = MakeService();

        var deactivated = await service.DeactivateAsync(
            f.EventId, f.AdminId, f.AdminId, true, "test");
        deactivated.IsSuccess.Should().BeTrue();

        var auditBefore = await Db.ConfigurationAuditLogs.CountAsync();

        var result = await service.ReactivateAsync(f.EventId, f.AdminId);

        result.IsSuccess.Should().BeTrue();

        var persisted = await Db.Events
            .AsNoTracking()
            .FirstAsync(e => e.EventId == f.EventId);
        persisted.IsActive.Should().BeTrue();
        persisted.IsDeleted.Should().BeFalse();

        var auditAfter = await Db.ConfigurationAuditLogs.CountAsync();
        auditAfter.Should().Be(auditBefore);
    }

    [Fact]
    public async Task Reactivate_SoftDeletedEvent_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        await service.SoftDeleteAsync(f.EventId, f.AdminId, f.AdminId, true, "test");

        var result = await service.ReactivateAsync(f.EventId, f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.LifecycleViolation);

        var persisted = await Db.Events
            .AsNoTracking()
            .FirstAsync(e => e.EventId == f.EventId);
        persisted.IsDeleted.Should().BeTrue();
    }

    [Fact]
    public async Task Reactivate_AlreadyActiveEvent_Fails()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.ReactivateAsync(f.EventId, f.AdminId);

        result.IsSuccess.Should().BeFalse();
        result.Error!.Code.Should().Be(ConfigurationErrorCodes.AlreadyActive);
    }
}
