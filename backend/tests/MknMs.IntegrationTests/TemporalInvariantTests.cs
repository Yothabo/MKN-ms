using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Operations.CreateManualAssignment;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Application.Processes.Operations.RecordAttendance;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Cross-cutting temporal invariants. Specification §4 (Temporal
/// types): timestamp columns represent an instant and are persisted
/// in UTC.
/// </summary>
/// <remarks>
/// The two tests here are representative coverage across two
/// processes. They do not prove that every timestamp written by every
/// process is UTC. A future process that writes a new timestamp
/// column should extend this file.
///
/// PostgreSQL's timestamptz stores an instant, not the original
/// offset. The assertion is on the value Npgsql returns to the
/// application, which is the contract the application reads. A
/// reading process must see an offset of zero, regardless of the
/// offset the writing process supplied.
/// </remarks>
public class TemporalInvariantTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now =
        new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly IDispatchNotificationService _notification = new FakeDispatchNotificationService();
    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    protected override async Task OnInitializedAsync()
    {
        // The per-class database is created by MigrateAsync but the
        // required settings are not seeded by any migration; the tests
        // below depend on ApplicationTimeZone and AttendanceRegisterEnabled
        // being present. Seed them here. The helper is idempotent.
        await TestSeed.EnsureRequiredSettingsAsync(Db);
    }

    private async Task<int> SeedOccurrenceAsync()
    {
        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var branch = new Branch { Name = "Main", Location = "Main Street" };

        Db.ServiceTypes.Add(serviceType);
        Db.TimeOfDays.Add(timeOfDay);
        Db.Branches.Add(branch);
        await Db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            Date = new DateOnly(2026, 10, 11),
            GeneratedBy = "System",
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        return occurrence.OccurrenceId;
    }

    private async Task<(int MemberId, int AdminId)> SeedMemberAndAdminAsync()
    {
        var role = new Role { Name = "Member" };
        Db.Roles.Add(role);

        var tier = new PermissionTier { Name = "Full Admin", IsDefault = true };
        Db.PermissionTiers.Add(tier);

        var branch = await Db.Branches.FirstAsync();

        await Db.SaveChangesAsync();

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var member = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
            ReceiptNumber = "R-0001",
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
    public async Task AttendanceRecord_Timestamp_IsUtc()
    {
        var occurrenceId = await SeedOccurrenceAsync();
        var (memberId, _) = await SeedMemberAndAdminAsync();

        var service = new RecordAttendanceService(Db, _clock);
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = memberId,
            OccurrenceId = occurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();

        Db.ChangeTracker.Clear();
        var row = await Db.AttendanceRecords
            .AsNoTracking()
            .FirstAsync(a => a.RecordId == result.RecordId);

        row.Timestamp.Offset.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public async Task RosterAssignment_CreatedAt_IsUtc()
    {
        var occurrenceId = await SeedOccurrenceAsync();
        var (memberId, adminId) = await SeedMemberAndAdminAsync();

        var duty = new Duty { Name = "Reading duty" };
        Db.Duties.Add(duty);
        await Db.SaveChangesAsync();

        var service = new CreateManualAssignmentService(
            Db, _notification, _clock, new OccurrenceLockService(Db));

        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = memberId,
            DutyId = duty.DutyId,
            OccurrenceId = occurrenceId,
            TargetStatusId = null,
            ActingAdminId = adminId,
        });

        result.Status.Should().Be("Success");

        Db.ChangeTracker.Clear();
        var row = await Db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == result.AssignmentId);

        row.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
    }
}
