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
public class TemporalInvariantTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now =
        new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private MknDbContext _db = null!;
    private IDispatchNotificationService _notification = null!;
    private TimeProvider _clock = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);
        _notification = new FakeDispatchNotificationService();
        _clock = new FakeTimeProvider(Now);

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE materializer_run, attendance_record, roster_assignment, " +
            "service_occurrence_duty, service_occurrence, service_schedule, " +
            "service_definition_duty, branch_time_slot, service_definition, " +
            "service_type, time_of_day, branch, identifier_history, eligibility, " +
            "duty_rule, admin, member, member_attribute_value, member_status, " +
            "attendance_rule_scope, attendance_rule, readmission, role, duty, " +
            "system_setting, outcome_state, assignment_status, permission_tier, " +
            "attribute_type, capability, event_branch, configuration_audit_log, " +
            "entity_deletion_policy, notification_subscription " +
            "RESTART IDENTITY CASCADE;");

        await TestSeed.EnsureRequiredSettingsAsync(_db);
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    private async Task<int> SeedOccurrenceAsync()
    {
        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var branch = new Branch { Name = "Main", Location = "Main Street" };

        _db.ServiceTypes.Add(serviceType);
        _db.TimeOfDays.Add(timeOfDay);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            Date = new DateOnly(2026, 10, 11),
            GeneratedBy = "System",
        };
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        return occurrence.OccurrenceId;
    }

    private async Task<(int MemberId, int AdminId)> SeedMemberAndAdminAsync()
    {
        var role = new Role { Name = "Member" };
        _db.Roles.Add(role);

        var tier = new PermissionTier { Name = "Full Admin", IsDefault = true };
        _db.PermissionTiers.Add(tier);

        var branch = await _db.Branches.FirstAsync();

        await _db.SaveChangesAsync();

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(_db);

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
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = member.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        return (member.MemberId, admin.AdminId);
    }

    [Fact]
    public async Task AttendanceRecord_Timestamp_IsUtc()
    {
        var occurrenceId = await SeedOccurrenceAsync();
        var (memberId, _) = await SeedMemberAndAdminAsync();

        var service = new RecordAttendanceService(_db, _clock);
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = memberId,
            OccurrenceId = occurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();

        _db.ChangeTracker.Clear();
        var row = await _db.AttendanceRecords
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
        _db.Duties.Add(duty);
        await _db.SaveChangesAsync();

        var service = new CreateManualAssignmentService(
            _db, _notification, _clock, new OccurrenceLockService(_db));

        var result = await service.RunAsync(new CreateManualAssignmentCommand
        {
            MemberId = memberId,
            DutyId = duty.DutyId,
            OccurrenceId = occurrenceId,
            TargetStatusId = null,
            ActingAdminId = adminId,
        });

        result.Status.Should().Be("Success");

        _db.ChangeTracker.Clear();
        var row = await _db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == result.AssignmentId);

        row.CreatedAt.Offset.Should().Be(TimeSpan.Zero);
    }
}
