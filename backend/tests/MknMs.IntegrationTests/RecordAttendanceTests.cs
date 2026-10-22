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
public class RecordAttendanceTests : IntegrationTestBase
{
    private readonly TimeProvider _clock = new FakeTimeProvider(
        new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));

    protected override async Task OnInitializedAsync()
    {
        // No migration seeds the required SystemSetting rows, and 6.0
        // depends on AttendanceRegisterEnabled and ApplicationTimeZone
        // being present. Seed them here per fact. The helper is
        // idempotent.
        await TestSeed.EnsureRequiredSettingsAsync(Db);
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

        Db.ServiceTypes.Add(serviceType);
        Db.TimeOfDays.Add(timeOfDay);
        Db.Roles.Add(role);
        Db.Branches.Add(branch);
        await Db.SaveChangesAsync();

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var member = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            ReceiptNumber = "R-0001",
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(member);
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
            OwningBranchId = null,
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
            EventId = null,
            Date = DateOnly.FromDateTime(occurrenceDate.Date),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        return new Fixture(member.MemberId, occurrence.OccurrenceId);
    }

    // -----------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------

    // Matrix row 6.0:cae53822d731: A member and an occurrence with no existing attendance record | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:cae53822d731")]
    [Trait("MatrixRow", "6.0:1e59cc45d2a2")]
    public async Task Record_NewPair_WritesRowAndReturnsRecorded()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(Db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();
        result.RecordId.Should().BeGreaterThan(0);

        var count = await Db.AttendanceRecords.CountAsync();
        count.Should().Be(1);

        var row = await Db.AttendanceRecords.FirstAsync();
        row.MemberId.Should().Be(f.MemberId);
        row.OccurrenceId.Should().Be(f.OccurrenceId);
        row.Timestamp.Should().Be(_clock.GetUtcNow());
    }

    // Matrix row 6.0:d19968af5847: A member and an occurrence with an existing attendance record | Attendance is recorded again
    [Fact]
    [Trait("MatrixRow", "6.0:d19968af5847")]
    public async Task Record_RepeatPair_IsIdempotent()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(Db, _clock);

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

        var count = await Db.AttendanceRecords.CountAsync();
        count.Should().Be(1);
    }

    // Matrix row 6.0:239739aa8f8a: A member with no RosterAssignment for the occurrence | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:239739aa8f8a")]
    public async Task Record_WithoutAnyAssignment_Succeeds()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(Db, _clock);

        // No RosterAssignment exists for this pair. §13.1.3: attendance
        // is not conditioned on assignment.
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();

        var assignmentCount = await Db.RosterAssignments.CountAsync();
        assignmentCount.Should().Be(0);
    }

    // Matrix row 6.0:cb41d9e29755: An occurrence dated in the future | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:cb41d9e29755")]
    public async Task Record_ForFutureOccurrence_Succeeds()
    {
        // A date far in the future. No attendance-window enforcement
        // exists in 6.0 or in the schema (§13.3).
        var farFuture = new DateTimeOffset(2030, 12, 25, 9, 0, 0, TimeSpan.Zero);
        var f = await SeedFixtureAsync(farFuture);
        var service = new RecordAttendanceService(Db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();
    }

    // Matrix row 6.0:24694d6ae73a: A missing member or occurrence | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:24694d6ae73a")]
    public async Task Record_WithMissingMember_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(Db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = 9999,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.Recorded.Should().BeFalse();
        result.ErrorDetail.Should().Contain("Member 9999");

        var count = await Db.AttendanceRecords.CountAsync();
        count.Should().Be(0);
    }

    // Matrix row 6.0:24694d6ae73a: A missing member or occurrence | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:24694d6ae73a")]
    public async Task Record_WithMissingOccurrence_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new RecordAttendanceService(Db, _clock);

        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = 9999,
        });

        result.Status.Should().Be("Failure");
        result.Recorded.Should().BeFalse();
        result.ErrorDetail.Should().Contain("ServiceOccurrence 9999");

        var count = await Db.AttendanceRecords.CountAsync();
        count.Should().Be(0);
    }

    // Matrix row 6.0:766946085973: A member with neither ReceiptNumber nor CardNumber | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:766946085973")]
    public async Task Record_WithMemberLackingIdentifier_Fails()
    {
        var f = await SeedFixtureAsync();

        var member = await Db.Members.FirstAsync();
        member.ReceiptNumber = null;
        member.CardNumber = null;
        await Db.SaveChangesAsync();

        var service = new RecordAttendanceService(Db, _clock);
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.Recorded.Should().BeFalse();
        result.ErrorDetail.Should().Contain("ReceiptNumber");

        var count = await Db.AttendanceRecords.CountAsync();
        count.Should().Be(0);
    }

    // Matrix row 6.0:032c4b2d2a18: An occurrence at a branch whose effective register scope is off | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:032c4b2d2a18")]
    public async Task Record_WithRegisterOffAtEffectiveScope_Fails()
    {
        var f = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "AttendanceRegisterEnabled");
        setting.Value = "false";
        await Db.SaveChangesAsync();

        var service = new RecordAttendanceService(Db, _clock);
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.Recorded.Should().BeFalse();
        result.ErrorDetail.Should().Contain("register is off");

        var count = await Db.AttendanceRecords.CountAsync();
        count.Should().Be(0);
    }

    // Matrix row 6.0:c250931c9094: A member attending at a branch other than their home branch | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:c250931c9094")]
    public async Task Record_AtNonHomeBranch_WritesAgainstOccurrence()
    {
        // §13.1.3: attendance is recorded against the occurrence, not
        // the member's home branch. Member.BranchID is not consulted.
        var f = await SeedFixtureAsync();

        // Add a second branch and move the member to it. The
        // occurrence's schedule still points at the first branch.
        var secondBranch = new Branch
        {
            Name = "Second",
            Location = "Elsewhere",
            IsActive = true,
        };
        Db.Branches.Add(secondBranch);
        await Db.SaveChangesAsync();

        var member = await Db.Members.FirstAsync();
        member.BranchId = secondBranch.BranchId;
        await Db.SaveChangesAsync();

        var service = new RecordAttendanceService(Db, _clock);
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();

        var row = await Db.AttendanceRecords.SingleAsync();
        row.MemberId.Should().Be(f.MemberId);
        row.OccurrenceId.Should().Be(f.OccurrenceId);
    }

    // Matrix row 6.0:ef252e6bcee5: An occurrence at a branch whose effective register scope is on | Attendance is recorded
    [Fact]
    [Trait("MatrixRow", "6.0:ef252e6bcee5")]
    public async Task Record_WithBranchOverrideOn_GlobalOff_WritesRow()
    {
        var f = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "AttendanceRegisterEnabled");
        setting.Value = "false";
        await Db.SaveChangesAsync();

        var branch = await Db.Branches.FirstAsync();
        branch.UsesAttendanceRegister = true;
        await Db.SaveChangesAsync();

        var service = new RecordAttendanceService(Db, _clock);
        var result = await service.RunAsync(new RecordAttendanceCommand
        {
            MemberId = f.MemberId,
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.Recorded.Should().BeTrue();
    }
}
