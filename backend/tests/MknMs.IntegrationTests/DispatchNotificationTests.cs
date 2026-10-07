using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D7_RosterAssignment;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 9.0 Dispatch Notification.
/// </summary>
/// <remarks>
/// The real DispatchNotificationService is exercised. Its transport is
/// structured logging (spec §14.1.2 places the channel mechanism
/// outside the schema), so a capturing ILogger stands in for the
/// destination.
///
/// §14.1.2 defines no channel-specific fallback. A NotificationChannel
/// that is absent and Required = true surfaces a configuration error; a
/// channel that is absent and Required = false, or a channel whose
/// value is not recognised, produces no send and no log. These tests
/// exercise all three of those cases.
/// </remarks>
public class DispatchNotificationTests : IAsyncLifetime
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

    private sealed record Fixture(
        RosterAssignment Assignment,
        int MemberId,
        int OccurrenceId,
        int DutyId);

    private async Task<Fixture> SeedFixtureAsync(
        string? notificationChannel = "Log",
        bool notificationChannelRequired = false)
    {
        var serviceType = new ServiceType { Name = "Regular" };
        var timeOfDay = new TimeOfDay { Name = "Morning" };
        var role = new Role { Name = "Coordinator", IsActive = true };
        var duty = new Duty { Name = "Reading duty", IsActive = true };
        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };

        _db.ServiceTypes.Add(serviceType);
        _db.TimeOfDays.Add(timeOfDay);
        _db.Roles.Add(role);
        _db.Duties.Add(duty);
        _db.Branches.Add(branch);
        await _db.SaveChangesAsync();

        if (notificationChannel is not null)
        {
            _db.SystemSettings.Add(new SystemSetting
            {
                Key = "NotificationChannel",
                Value = notificationChannel,
                Required = notificationChannelRequired,
            });
            await _db.SaveChangesAsync();
        }

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
            StartTime = new TimeOnly(9, 30),
            IsActive = true,
        };
        _db.ServiceSchedules.Add(schedule);
        await _db.SaveChangesAsync();

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = new TimeOnly(9, 30),
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        var assignment = new RosterAssignment
        {
            MemberId = member.MemberId,
            DutyId = duty.DutyId,
            OccurrenceId = occurrence.OccurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = DateTimeOffset.UtcNow,
        };
        _db.RosterAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        return new Fixture(
            assignment,
            member.MemberId,
            occurrence.OccurrenceId,
            duty.DutyId);
    }

    [Fact]
    public async Task Dispatch_WithAllReferents_LogsComposedMessage()
    {
        var f = await SeedFixtureAsync();
        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(_db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().HaveCount(1);
        var entry = logger.Entries[0];
        entry.Level.Should().Be(LogLevel.Information);
        entry.Message.Should().Contain("Alex Example");
        entry.Message.Should().Contain("Reading duty");
        entry.Message.Should().Contain("2026-10-11");
        entry.Message.Should().Contain("09:30");
    }

    [Fact]
    public async Task Dispatch_WithMissingMember_StillLogsWithFallback()
    {
        var f = await SeedFixtureAsync();

        // Delete the member's children, then the member. The FK
        // constraints are ON DELETE RESTRICT by design; the delete
        // order matters.
        var assignments = await _db.RosterAssignments
            .Where(a => a.MemberId == f.MemberId)
            .ToListAsync();
        _db.RosterAssignments.RemoveRange(assignments);
        await _db.SaveChangesAsync();

        var member = await _db.Members.FirstAsync(m => m.MemberId == f.MemberId);
        _db.Members.Remove(member);
        await _db.SaveChangesAsync();

        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(_db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].Message.Should().Contain($"member #{f.MemberId}");
    }

    [Fact]
    public async Task Dispatch_WithMissingOccurrence_StillLogsWithFallback()
    {
        var f = await SeedFixtureAsync();

        // Same treatment: the occurrence is referenced by the
        // RosterAssignment. Delete the child first.
        var assignments = await _db.RosterAssignments
            .Where(a => a.OccurrenceId == f.OccurrenceId)
            .ToListAsync();
        _db.RosterAssignments.RemoveRange(assignments);
        await _db.SaveChangesAsync();

        var occurrence = await _db.ServiceOccurrences
            .FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        _db.ServiceOccurrences.Remove(occurrence);
        await _db.SaveChangesAsync();

        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(_db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].Message.Should().Contain("unknown date");
    }

    [Fact]
    public async Task Dispatch_WithChannelSettingAbsentAndNotRequired_DoesNotLog()
    {
        // §14.1.2: absent and Required = false → no notification.
        var f = await SeedFixtureAsync(notificationChannel: null);
        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(_db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().BeEmpty(
            "an absent optional NotificationChannel must not log (§14.1.2)");
    }

    [Fact]
    public async Task Dispatch_WithUnimplementedChannel_DoesNotLog()
    {
        // §14.1.2: an unrecognised channel value produces no send and no
        // log. There is no fallback to logging.
        var f = await SeedFixtureAsync(notificationChannel: "Email");
        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(_db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().BeEmpty(
            "an unimplemented channel must not silently log (§14.1.2)");
    }

    [Fact]
    public async Task Dispatch_WritesNothing()
    {
        var f = await SeedFixtureAsync();

        var assignmentBefore = await _db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == f.Assignment.AssignmentId);

        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(_db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        _db.ChangeTracker.Clear();

        var assignmentAfter = await _db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == f.Assignment.AssignmentId);

        assignmentAfter.AssignmentStatusId.Should().Be(assignmentBefore.AssignmentStatusId);
        assignmentAfter.CreatedAt.Should().Be(assignmentBefore.CreatedAt);

        var assignmentCount = await _db.RosterAssignments.CountAsync();
        assignmentCount.Should().Be(1);

        var settingCount = await _db.SystemSettings.CountAsync();
        settingCount.Should().Be(1);
    }
}

/// <summary>
/// ILogger that captures every emitted entry so a test can assert on
/// the composed message and level. Used by DispatchNotificationTests,
/// whose transport is structured logging.
/// </summary>
internal sealed class CapturingLogger<T> : ILogger<T>
{
    public List<(LogLevel Level, string Message)> Entries { get; } = new();

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        Entries.Add((logLevel, message));
    }
}
