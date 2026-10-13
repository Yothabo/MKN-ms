using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Configuration.ManageEvent;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D9_EventProgram;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Tests for ManageEventService.SoftDeleteAsync and the Event
/// cascade.
/// </summary>
public class ManageEventServiceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 13, 22, 30, 0, TimeSpan.Zero);

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
            "member, member_status, branch, role, event, admin, permission_tier " +
            "RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

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
        var status = new MemberStatus
        {
            Name = "Active", IsRosterable = true, IsDefault = true, IsDeleted = false,
        };
        var tier = new PermissionTier
        {
            Name = "Full Admin", IsDefault = true, IsDeleted = false,
        };
        var duty = new Duty { Name = "MC duty", IsActive = true, IsDeleted = false };

        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        _db.MemberStatuses.Add(status);
        _db.PermissionTiers.Add(tier);
        _db.Duties.Add(duty);
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
            Name = "Conference",
            StartDate = new DateOnly(2026, 11, 1),
            EndDate = new DateOnly(2026, 11, 3),
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

        var eventDuty = new EventDuty
        {
            ProgramItemId = item.ItemId,
            DutyId = duty.DutyId,
            AssignedMemberId = member.MemberId,
            IsDeleted = false,
        };
        _db.EventDuties.Add(eventDuty);
        await _db.SaveChangesAsync();

        return new Fixture(
            evt.EventId,
            admin.AdminId,
            program.ProgramId,
            item.ItemId,
            eventDuty.EventDutyId);
    }

    private ManageEventService MakeService() =>
        new(_db,
            new ConfigurationLifecycleService(_db, _clock),
            new ConfigurationCascadeService(_db));

    [Fact]
    public async Task SoftDelete_RemovesProgramItemEventDutyCascade()
    {
        var f = await SeedAsync();
        var service = MakeService();

        var result = await service.SoftDeleteAsync(
            f.EventId, f.AdminId, f.AdminId, true, "retired");

        result.IsSuccess.Should().BeTrue();

        (await _db.EventDuties.CountAsync()).Should().Be(0);
        (await _db.ProgramItems.CountAsync()).Should().Be(0);
        (await _db.Programs.CountAsync()).Should().Be(0);

        var evt = await _db.Events
            .AsNoTracking()
            .FirstAsync(e => e.EventId == f.EventId);
        evt.IsDeleted.Should().BeTrue();
        evt.IsActive.Should().BeFalse();

        var audit = await _db.ConfigurationAuditLogs.SingleAsync();
        audit.ConsequencesPreviewed.Should().Contain("removed");
    }

    [Fact]
    public async Task SoftDelete_PreservesEventSourcedOccurrences()
    {
        var f = await SeedAsync();

        var st = new ServiceType { Name = "Regular" };
        _db.ServiceTypes.Add(st);
        await _db.SaveChangesAsync();

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
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        var service = MakeService();
        await service.SoftDeleteAsync(
            f.EventId, f.AdminId, f.AdminId, true, "retired");

        var stillThere = await _db.ServiceOccurrences
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
}
