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
///
/// The sweep's per-recipient dispatch-failure branch (acceptance matrix
/// row 8) is not tested here. The sweep's only per-recipient operation
/// today is logging; a real transport, when added, introduces the
/// failure path. Adding a test seam now would create a production
/// abstraction with no current functional need.
/// </remarks>
public class DispatchNotificationTests : IntegrationTestBase
{
    protected override async Task OnInitializedAsync()
    {
        // No migration seeds SystemSetting. 9.0 depends on
        // NotificationChannel being present (or explicitly absent, which
        // the tests set up themselves) and the surrounding fixture needs
        // ApplicationTimeZone and AttendanceRegisterEnabled. Seed the
        // required rows per fact. The helper is idempotent.
        await TestSeed.EnsureRequiredSettingsAsync(Db);
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

        Db.ServiceTypes.Add(serviceType);
        Db.TimeOfDays.Add(timeOfDay);
        Db.Roles.Add(role);
        Db.Duties.Add(duty);
        Db.Branches.Add(branch);
        await Db.SaveChangesAsync();

        if (notificationChannel is not null)
        {
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "NotificationChannel",
                Value = notificationChannel,
                Required = notificationChannelRequired,
            });
            await Db.SaveChangesAsync();
        }

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var member = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
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
            StartTime = new TimeOnly(9, 30),
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

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
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

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
        Db.RosterAssignments.Add(assignment);
        await Db.SaveChangesAsync();

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
        var service = new DispatchNotificationService(Db, logger);

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
        var assignments = await Db.RosterAssignments
            .Where(a => a.MemberId == f.MemberId)
            .ToListAsync();
        Db.RosterAssignments.RemoveRange(assignments);
        await Db.SaveChangesAsync();

        var member = await Db.Members.FirstAsync(m => m.MemberId == f.MemberId);
        Db.Members.Remove(member);
        await Db.SaveChangesAsync();

        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(Db, logger);

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
        var assignments = await Db.RosterAssignments
            .Where(a => a.OccurrenceId == f.OccurrenceId)
            .ToListAsync();
        Db.RosterAssignments.RemoveRange(assignments);
        await Db.SaveChangesAsync();

        var occurrence = await Db.ServiceOccurrences
            .FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        Db.ServiceOccurrences.Remove(occurrence);
        await Db.SaveChangesAsync();

        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(Db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().HaveCount(1);
        logger.Entries[0].Message.Should().Contain("unknown date");
    }

    // Matrix row 9.0:63893ab2ecd9: NotificationChannel absent and Required = false | Dispatch is attempted
    [Fact]
    [Trait("MatrixRow", "9.0:63893ab2ecd9")]
    public async Task Dispatch_WithChannelSettingAbsentAndNotRequired_DoesNotLog()
    {
        // §14.1.2: absent and Required = false → no notification.
        var f = await SeedFixtureAsync(notificationChannel: null);
        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(Db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().BeEmpty(
            "an absent optional NotificationChannel must not log (§14.1.2)");
    }

    // Matrix row 9.0:99deb4ded8ce: NotificationChannel absent and Required = true | Dispatch is attempted
    [Fact]
    [Trait("MatrixRow", "9.0:99deb4ded8ce")]
    public async Task Dispatch_WithRequiredChannelAbsent_Throws()
    {
        // §14.1.2: absent and Required = true → the invocation refuses
        // and surfaces a configuration error. The service rethrows the
        // InvalidOperationException so the invoking process can see it.
        var f = await SeedFixtureAsync(
            notificationChannel: null,
            notificationChannelRequired: false);

        // SeedFixtureAsync skips the setting when the value is null.
        // Insert it explicitly with Required = true.
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "NotificationChannel",
            Value = null,
            Required = true,
        });
        await Db.SaveChangesAsync();

        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(Db, logger);

        var act = async () => await service.DispatchAssignmentNoticeAsync(f.Assignment);

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*NotificationChannel*");
    }

    // Matrix row 9.0:a52c7271998d: NotificationChannel set to an unrecognised value | Dispatch is attempted
    [Fact]
    [Trait("MatrixRow", "9.0:a52c7271998d")]
    public async Task Dispatch_WithUnimplementedChannel_DoesNotLog()
    {
        // §14.1.2: an unrecognised channel value produces no send and no
        // log. There is no fallback to logging.
        var f = await SeedFixtureAsync(notificationChannel: "Email");
        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(Db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        logger.Entries.Should().BeEmpty(
            "an unimplemented channel must not silently log (§14.1.2)");
    }

    [Fact]
    public async Task Dispatch_WritesNothing()
    {
        var f = await SeedFixtureAsync();

        var assignmentBefore = await Db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == f.Assignment.AssignmentId);

        var settingCountBefore = await Db.SystemSettings.CountAsync();

        var logger = new CapturingLogger<DispatchNotificationService>();
        var service = new DispatchNotificationService(Db, logger);

        await service.DispatchAssignmentNoticeAsync(f.Assignment);

        Db.ChangeTracker.Clear();

        var assignmentAfter = await Db.RosterAssignments
            .AsNoTracking()
            .FirstAsync(a => a.AssignmentId == f.Assignment.AssignmentId);

        assignmentAfter.AssignmentStatusId.Should().Be(assignmentBefore.AssignmentStatusId);
        assignmentAfter.CreatedAt.Should().Be(assignmentBefore.CreatedAt);

        var assignmentCount = await Db.RosterAssignments.CountAsync();
        assignmentCount.Should().Be(1);

        // The dispatch writes nothing, so the SystemSetting count is
        // unchanged by the call. TestSeed inserted the required
        // settings during the fixture; the absolute count is not
        // fixed, so assert the count did not grow.
        var settingCount = await Db.SystemSettings.CountAsync();
        settingCount.Should().Be(settingCountBefore);
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
