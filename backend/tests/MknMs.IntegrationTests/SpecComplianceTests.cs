using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.GenerateAssignment;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D6_Eligibility;
using MknMs.Domain.D7_RosterAssignment;
using MknMs.Domain.D8_AttendanceRecord;
using MknMs.Persistence;
using Xunit;

namespace MknMs.IntegrationTests;

/// <summary>
/// Three tests isolating three specification deviations found by review.
/// Written against the current code, before the fix. Each fails if the
/// defect exists and passes once the code matches the specification.
///
/// Specification: 5.0 doc lines 62-66, line 97; 9.0 doc lines 66, 70.
/// </summary>
public class SpecComplianceTests : IAsyncLifetime
{
    private const string TestConnectionString =
        "Host=127.0.0.1;Port=5432;Database=mkn_test;Username=mkn_dev";

    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private MknDbContext _db = null!;
    private FakeDispatchNotificationService _notification = null!;
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
            "duty_rule, admin, member, role, duty, system_setting, " +
            "outcome_state, assignment_status, permission_tier RESTART IDENTITY CASCADE;");
    }

    public Task DisposeAsync()
    {
        _db.Dispose();
        return Task.CompletedTask;
    }

    // -----------------------------------------------------------------
    // Shared seed helper
    // -----------------------------------------------------------------

    private async Task<(int OccurrenceId, int DutyId, int MemberId, int AdminId, int BranchId)> SeedBasicAsync(
        string branchName = "Main")
    {
        _db.ServiceTypes.Add(new ServiceType { Name = "Regular" });
        _db.TimeOfDays.Add(new TimeOfDay { Name = "Slot" });
        var role = new Role { Name = "Member", IsActive = true };
        _db.Roles.Add(role);
        var duty = new Duty { Name = "Reading duty", IsActive = true };
        _db.Duties.Add(duty);
        var branch = new Branch { Name = branchName, Location = "Here", IsActive = true };
        _db.Branches.Add(branch);
        var tier = new PermissionTier { Name = "Full Admin" };
        _db.PermissionTiers.Add(tier);
        await _db.SaveChangesAsync();

        var alex = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId, IsActive = true,
        };
        _db.Members.Add(alex);
        await _db.SaveChangesAsync();

        var admin = new Admin { MemberId = alex.MemberId, PermissionTierId = tier.PermissionTierId };
        _db.Admins.Add(admin);
        await _db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = _db.TimeOfDays.Local.First().TimeOfDayId,
            IsActive = true,
        };
        _db.BranchTimeSlots.Add(slot);
        await _db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Service",
            ServiceTypeId = _db.ServiceTypes.Local.First().ServiceTypeId,
            OwningBranchId = null,
            IsActive = true,
        };
        _db.ServiceDefinitions.Add(definition);
        await _db.SaveChangesAsync();

        _db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty.DutyId,
            RequiredSlotCount = 1,
            IsActive = true,
        });

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
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(occurrence);
        await _db.SaveChangesAsync();

        return (occurrence.OccurrenceId, duty.DutyId, alex.MemberId, admin.AdminId, branch.BranchId);
    }

    // -----------------------------------------------------------------
    // Item 1 — future grant must not win eligibility
    // -----------------------------------------------------------------

    [Fact]
    public async Task Eligibility_FutureGrant_DoesNotMakeMemberEligible()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        // Two grants for the same (member, duty):
        //   A: GrantedDate 2026-10-01, revoked null       — current
        //   B: GrantedDate 2026-10-20, revoked null       — future
        // The 5.0 doc says: current row wins. The code currently picks B
        // because B has the later GrantedDate. Under the spec, the member
        // is eligible (A is current). But if B alone existed with a
        // future date and A did not, the member would not be eligible.
        //
        // Test: only a future grant exists. The member must NOT be
        // eligible.
        _db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 10, 20),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        await _db.SaveChangesAsync();

        var service = new GenerateAssignmentService(_db, _notification, _clock);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        // §10.1.4: a member becomes eligible only when GrantedDate is in
        // the past. A future grant alone does not make them eligible.
        var assignments = await _db.RosterAssignments
            .Where(a => a.MemberId == memberId)
            .CountAsync();

        assignments.Should().Be(0,
            "a future GrantedDate must not make a member eligible (§10.1.4)");
    }

    [Fact]
    public async Task Eligibility_FutureRevoke_DoesNotRevoke()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        // A current grant with a RevokedDate in the future. §10.1.4:
        // a future RevokedDate does not revoke. The member is currently
        // eligible.
        _db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 1, 1),
            GrantedBy = adminId,
            RevokedDate = new DateOnly(2027, 1, 1),
            RevokedReason = null,
        });
        await _db.SaveChangesAsync();

        var service = new GenerateAssignmentService(_db, _notification, _clock);
        await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        var assignments = await _db.RosterAssignments
            .Where(a => a.MemberId == memberId)
            .CountAsync();

        assignments.Should().Be(1,
            "a future RevokedDate must not revoke a grant (§10.1.4)");
    }

    // -----------------------------------------------------------------
    // Item 2 — Branch-Attendance Recency must filter by branch
    // -----------------------------------------------------------------

    [Fact]
    public async Task BranchAttendanceRecency_IgnoresAttendanceAtWrongBranch()
    {
        // Two branches. Duty rule requires attendance at "Target".
        // Member attended only at "Other". Criterion must not match, so
        // the duty stays unfilled.
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync(branchName: "Target");

        // A second branch.
        var otherBranch = new Branch { Name = "Other", Location = "Elsewhere", IsActive = true };
        _db.Branches.Add(otherBranch);
        await _db.SaveChangesAsync();

        var otherSlot = new BranchTimeSlot
        {
            BranchId = otherBranch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = _db.TimeOfDays.Local.First().TimeOfDayId,
            IsActive = true,
        };
        _db.BranchTimeSlots.Add(otherSlot);
        await _db.SaveChangesAsync();

        var otherDefinition = new ServiceDefinition
        {
            Name = "Other Service",
            ServiceTypeId = _db.ServiceTypes.Local.First().ServiceTypeId,
            OwningBranchId = null,
            IsActive = true,
        };
        _db.ServiceDefinitions.Add(otherDefinition);
        await _db.SaveChangesAsync();

        var otherSchedule = new ServiceSchedule
        {
            ServiceDefId = otherDefinition.ServiceDefId,
            TimeSlotId = otherSlot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
            IsActive = true,
        };
        _db.ServiceSchedules.Add(otherSchedule);
        await _db.SaveChangesAsync();

        var otherOccurrence = new ServiceOccurrence
        {
            ScheduleId = otherSchedule.ScheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 6),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        _db.ServiceOccurrences.Add(otherOccurrence);
        await _db.SaveChangesAsync();

        // Attendance at the OTHER branch.
        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            MemberId = memberId,
            OccurrenceId = otherOccurrence.OccurrenceId,
            Timestamp = Now.AddDays(-1),
        });

        // Eligibility + DutyRule: Branch-Attendance Recency, value "Target".
        _db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 1, 1),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        _db.DutyRules.Add(new DutyRule
        {
            DutyId = dutyId,
            TierOrder = 1,
            CriteriaType = "BranchAttendanceRecency",
            CriteriaValue = "30",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        var service = new GenerateAssignmentService(_db, _notification, _clock);
        await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        var assignments = await _db.RosterAssignments
            .Where(a => a.MemberId == memberId)
            .CountAsync();

        assignments.Should().Be(0,
            "attendance at a different branch must not satisfy the criterion (5.0 doc line 97)");
    }

    [Fact]
    public async Task BranchAttendanceRecency_MatchesAttendanceAtRightBranch()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync(branchName: "Target");

        // Attendance at the SAME branch as the occurrence.
        var occurrence = await _db.ServiceOccurrences
            .FirstAsync(o => o.OccurrenceId == occurrenceId);

        _db.AttendanceRecords.Add(new AttendanceRecord
        {
            MemberId = memberId,
            OccurrenceId = occurrence.OccurrenceId,
            Timestamp = Now.AddDays(-1),
        });

        _db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 1, 1),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        _db.DutyRules.Add(new DutyRule
        {
            DutyId = dutyId,
            TierOrder = 1,
            CriteriaType = "BranchAttendanceRecency",
            CriteriaValue = "30",
            IsActive = true,
        });
        await _db.SaveChangesAsync();

        var service = new GenerateAssignmentService(_db, _notification, _clock);
        await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        var assignments = await _db.RosterAssignments
            .Where(a => a.MemberId == memberId)
            .CountAsync();

        assignments.Should().Be(1,
            "attendance at the specified branch must satisfy the criterion (5.0 doc line 97)");
    }

    // -----------------------------------------------------------------
    // Item 3 — NotificationChannel fallback
    // -----------------------------------------------------------------

    [Fact]
    public async Task Notification_WithRequiredChannelAbsent_MustNotDispatch()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        // Required setting, absent. §14.1.2 + 9.0 doc line 70:
        // 9.0 refuses to send and surfaces a configuration error.
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "NotificationChannel",
            Value = null,
            Required = true,
        });
        await _db.SaveChangesAsync();

        var notification = new FakeDispatchNotificationService();
        var service = new MknMs.Application.Processes.Operations.DispatchNotification
            .DispatchNotificationService(_db, new CapturingLogger<MknMs.Application.Processes.Operations.DispatchNotification.DispatchNotificationService>());

        var assignment = new RosterAssignment
        {
            MemberId = memberId,
            DutyId = dutyId,
            OccurrenceId = occurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = Now,
        };
        _db.RosterAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        var act = async () => await service.DispatchAssignmentNoticeAsync(assignment);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a required NotificationChannel that is absent must surface a configuration error (9.0 doc line 70)");
    }

    [Fact]
    public async Task Notification_WithOptionalChannelAbsent_MustNotLog()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        // Optional setting, absent. §14.1.2 + 9.0 doc line 70:
        // no notification is sent.
        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "NotificationChannel",
            Value = null,
            Required = false,
        });
        await _db.SaveChangesAsync();

        var logger = new CapturingLogger<MknMs.Application.Processes.Operations.DispatchNotification.DispatchNotificationService>();
        var service = new MknMs.Application.Processes.Operations.DispatchNotification
            .DispatchNotificationService(_db, logger);

        var assignment = new RosterAssignment
        {
            MemberId = memberId,
            DutyId = dutyId,
            OccurrenceId = occurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = Now,
        };
        _db.RosterAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        await service.DispatchAssignmentNoticeAsync(assignment);

        logger.Entries.Should().BeEmpty(
            "an optional NotificationChannel that is absent must not send and must not log a notification (9.0 doc line 70)");
    }

    [Fact]
    public async Task Notification_WithInvalidChannel_MustNotLog()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        _db.SystemSettings.Add(new SystemSetting
        {
            Key = "NotificationChannel",
            Value = "Not/AChannel",
            Required = false,
        });
        await _db.SaveChangesAsync();

        var logger = new CapturingLogger<MknMs.Application.Processes.Operations.DispatchNotification.DispatchNotificationService>();
        var service = new MknMs.Application.Processes.Operations.DispatchNotification
            .DispatchNotificationService(_db, logger);

        var assignment = new RosterAssignment
        {
            MemberId = memberId,
            DutyId = dutyId,
            OccurrenceId = occurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = Now,
        };
        _db.RosterAssignments.Add(assignment);
        await _db.SaveChangesAsync();

        await service.DispatchAssignmentNoticeAsync(assignment);

        logger.Entries.Should().BeEmpty(
            "an invalid NotificationChannel must not silently log (9.0 doc line 66)");
    }
}
