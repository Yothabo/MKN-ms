using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.RecordAttendance;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 6.0 Record Attendance.
/// </summary>
/// <remarks>
/// Runs against mkn_test. Exercises:
///   - A single attendance record is written for a (MemberId,
///     OccurrenceId) pair.
///   - Repeat invocations for the same pair are idempotent — no second
///     row, the existing row is returned.
///   - Attendance does not depend on assignment.
///   - No attendance-window enforcement.
///   - Missing member or occurrence fails cleanly.
///
/// Reference: docs/processes/operations/6.0-record-attendance.md,
/// Specification §13.
/// </remarks>
public class RecordAttendanceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private MknDbContext _db = null!;
    private TimeProvider _clock = null!;

    public async Task InitializeAsync()
    {
        var options = new DbContextOptionsBuilder<MknDbContext>()
            .UseNpgsql(TestConnectionString)
            .Options;

        _db = new MknDbContext(options);
        _clock = new FakeTimeProvider(new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));

        await _db.Database.ExecuteSqlRawAsync(
            "TRUNCATE TABLE materializer_run, attendance_record, roster_assignment, " +
            "service_occurrence_duty, service_occurrence, service_schedule, " +
            "service_definition_duty, branch_time_slot, service_definition, " +
            "service_type, time_of_day, branch, identifier_history, eligibility, " +
            "duty_rule, admin, member, role, duty, system_setting, " +
            "outcome_state, assignment_status, permission_tier RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------

    private sealed record Fixture(int MemberId, int OccurrenceId);

    private async Task<Fixture> SeedFixtureAsync(DateTimeOffset occurrenceDate = default)
    {
        if (occurrenceDate == default)
        {
            occurrenceDate = new DateTimeOffset(2026, 10, 11, 9, 0, 0, TimeSpan.Zero);
        }

        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var role = new Role { Name = "Coordinator", IsActive = true };
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };

        _db.ServiceTypes.Add(serviceType);
        _db.TimeOfDays.Add(timeOfDay);
        _db.Roles.Add(role);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        var member = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId, IsActive = true,
        };
        _db.Members.Add(member);
        await _db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = timeOfDay.TimeOfDayId,
            IsActive = true,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Sunday Service",
            ServiceTypeId = serviceType.ServiceTypeId,
            OwningBranchId = null,
            IsActive = true,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsActive = true,
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = DateOnly.FromDateTime(occurrenceDate.Date),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        return new Fixture(member.MemberId, occurrence.OccurrenceId);
    }

    // -----------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------

    [Fact]
    public async Task Record_NewPair_WritesRowAndReturnsRecorded()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(_db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();
        result.RecordId.Should().BeGreaterThan(0);

        var count = await _db.AttendanceRecords.CountAsync();
        count.Should().Be(1);

        var row = await _db.AttendanceRecords.FirstAsync();
        row.MemberId.Should().Be(f.MemberId);
        row.OccurrenceId.Should().Be(f.OccurrenceId);
        row.Timestamp.Should().Be(_clock.GetUtcNow());
    }

    [Fact]
    public async Task Record_RepeatPair_IsIdempotent()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(_db, _clock);

        var first = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        var second = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        first.Recorded.Should().BeTrue();
        second.Recorded.Should().BeFalse();
        second.RecordId.Should().Be(first.RecordId);

        var count = await _db.AttendanceRecords.CountAsync();
        count.Should().Be(1);
    }

    [Fact]
    public async Task Record_WithoutAnyAssignment_Succeeds()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(_db, _clock);

        // No RosterAssignment exists for this pair. §13.1.3: attendance
        // is not conditioned on assignment.
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();

        var assignmentCount = await _db.RosterAssignments.CountAsync();
        assignmentCount.Should().Be(0);
    }

    [Fact]
    public async Task Record_ForFutureOccurrence_Succeeds()
    {
        // A date far in the future. No attendance-window enforcement
        // exists in 6.0 or in the schema (§13.3).
        var farFuture = new DateTimeOffset(2030, 12, 25, 9, 0, 0, TimeSpan.Zero);
        var f = await SeedFixtureAsync(farFuture);
        var service = new RecordAttendanceService(_db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();
    }

    [Fact]
    public async Task Record_WithMissingMember_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(_db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = 9999,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.Recorded.Should().BeFalse();
        result.ErrorDetail.Should().Contain("Member 9999");

        var count = await _db.AttendanceRecords.CountAsync();
        count.Should().Be(0);
    }

    [Fact]
    public async Task Record_WithMissingOccurrence_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(_db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = 9999,
        });

        result.Status.Should().Be("Failure");
        result.Recorded.Should().BeFalse();
        result.ErrorDetail.Should().Contain("ServiceOccurrence 9999");

        var count = await _db.AttendanceRecords.CountAsync();
        count.Should().Be(0);
    }
}
