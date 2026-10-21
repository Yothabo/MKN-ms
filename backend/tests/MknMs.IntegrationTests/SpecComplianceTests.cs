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
using MknMs.Application.Common;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace MknMs.IntegrationTests;

/// <summary>
/// Three tests isolating three specification deviations found by review.
/// Written against the current code, before the fix. Each fails if the
/// defect exists and passes once the code matches the specification.
///
/// Specification: 5.0 doc lines 62-66, line 97; 9.0 doc lines 66, 70.
/// </summary>
public class SpecComplianceTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 10, 7, 12, 0, 0, TimeSpan.Zero);

    private readonly FakeDispatchNotificationService _notification = new FakeDispatchNotificationService();
    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    protected override async Task OnInitializedAsync()
    {
        await TestSeed.EnsureRequiredSettingsAsync(Db);
    }

    // -----------------------------------------------------------------
    // Shared seed helper
    // -----------------------------------------------------------------

    private async Task<(int OccurrenceId, int DutyId, int MemberId, int AdminId, int BranchId)> SeedBasicAsync(
        string branchName = "Main")
    {
        Db.ServiceTypes.Add(new ServiceType { Name = "Regular" });
        Db.TimeOfDays.Add(new TimeOfDay { Name = "Slot" });
        var role = new Role { Name = "Member", IsActive = true };
        Db.Roles.Add(role);
        var duty = new Duty { Name = "Reading duty", IsActive = true };
        Db.Duties.Add(duty);
        var branch = new Branch { Name = branchName, Location = "Here", IsActive = true };
        Db.Branches.Add(branch);
        var tier = new PermissionTier { Name = "Full Admin" };
        Db.PermissionTiers.Add(tier);
        await Db.SaveChangesAsync();

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var alex = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 1),
            DateOfBirth = new DateOnly(1990, 1, 1),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(alex);
        await Db.SaveChangesAsync();

        var admin = new Admin { MemberId = alex.MemberId, PermissionTierId = tier.PermissionTierId };
        Db.Admins.Add(admin);
        await Db.SaveChangesAsync();

        var slot = new BranchTimeSlot
        {
            BranchId = branch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = Db.TimeOfDays.Local.First().TimeOfDayId,
        };
        Db.BranchTimeSlots.Add(slot);
        await Db.SaveChangesAsync();

        var definition = new ServiceDefinition
        {
            Name = "Service",
            ServiceTypeId = Db.ServiceTypes.Local.First().ServiceTypeId,
            OwningBranchId = null,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = duty.DutyId,
            RequiredSlotCount = 1,
        });

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
            Date = new DateOnly(2026, 10, 11),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        return (occurrence.OccurrenceId, duty.DutyId, alex.MemberId, admin.AdminId, branch.BranchId);
    }

    // -----------------------------------------------------------------
    // Item 1 — future grant must not win eligibility
    // -----------------------------------------------------------------

    // Matrix row 5.0:9107c30cb4e3: A duty with no eligible members | 5.0 runs
    [Fact]
    [Trait("MatrixRow", "5.0:9107c30cb4e3")]
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
        Db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 10, 20),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        // §10.1.4: a member becomes eligible only when GrantedDate is in
        // the past. A future grant alone does not make them eligible.
        var assignments = await Db.RosterAssignments
            .Where(a => a.MemberId == memberId)
            .CountAsync();

        assignments.Should().Be(0,
            "a future GrantedDate must not make a member eligible (§10.1.4)");
    }

    // Matrix row 5.0:9107c30cb4e3: A duty with no eligible members | 5.0 runs
    [Fact]
    [Trait("MatrixRow", "5.0:9107c30cb4e3")]
    public async Task Eligibility_FutureRevoke_DoesNotRevoke()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        // A current grant with a RevokedDate in the future. §10.1.4:
        // a future RevokedDate does not revoke. The member is currently
        // eligible.
        Db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 1, 1),
            GrantedBy = adminId,
            RevokedDate = new DateOnly(2027, 1, 1),
            RevokedReason = null,
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        var assignments = await Db.RosterAssignments
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
        Db.Branches.Add(otherBranch);
        await Db.SaveChangesAsync();

        var otherSlot = new BranchTimeSlot
        {
            BranchId = otherBranch.BranchId,
            DayOfWeek = "Sunday",
            TimeOfDayId = Db.TimeOfDays.Local.First().TimeOfDayId,
        };
        Db.BranchTimeSlots.Add(otherSlot);
        await Db.SaveChangesAsync();

        var otherDefinition = new ServiceDefinition
        {
            Name = "Other Service",
            ServiceTypeId = Db.ServiceTypes.Local.First().ServiceTypeId,
            OwningBranchId = null,
        };
        Db.ServiceDefinitions.Add(otherDefinition);
        await Db.SaveChangesAsync();

        var otherSchedule = new ServiceSchedule
        {
            ServiceDefId = otherDefinition.ServiceDefId,
            TimeSlotId = otherSlot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        Db.ServiceSchedules.Add(otherSchedule);
        await Db.SaveChangesAsync();

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
        Db.ServiceOccurrences.Add(otherOccurrence);
        await Db.SaveChangesAsync();

        // Attendance at the OTHER branch.
        Db.AttendanceRecords.Add(new AttendanceRecord
        {
            MemberId = memberId,
            OccurrenceId = otherOccurrence.OccurrenceId,
            Timestamp = Now.AddDays(-1),
            Source = "Manual",
        });

        // Eligibility + DutyRule: Branch-Attendance Recency, value "Target".
        Db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 1, 1),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        Db.DutyRules.Add(new DutyRule
        {
            DutyId = dutyId,
            TierOrder = 1,
            CriteriaType = "BranchAttendanceRecency",
            CriteriaValue = "30",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        var assignments = await Db.RosterAssignments
            .Where(a => a.MemberId == memberId)
            .CountAsync();

        assignments.Should().Be(0,
            "attendance at a different branch must not satisfy the criterion (5.0 doc line 97)");
    }

    // Matrix row 5.0:6553f36fc96a: A Branch-Attendance Recency rule on a duty, with the effective register scope on and a matching AttendanceRecord | 5.0 runs
    [Fact]
    [Trait("MatrixRow", "5.0:6553f36fc96a")]
    public async Task BranchAttendanceRecency_MatchesAttendanceAtRightBranch()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync(branchName: "Target");

        // Attendance at the SAME branch as the occurrence.
        var occurrence = await Db.ServiceOccurrences
            .FirstAsync(o => o.OccurrenceId == occurrenceId);

        Db.AttendanceRecords.Add(new AttendanceRecord
        {
            MemberId = memberId,
            OccurrenceId = occurrence.OccurrenceId,
            Timestamp = Now.AddDays(-1),
            Source = "Manual",
        });

        Db.Eligibilities.Add(new Eligibility
        {
            MemberId = memberId,
            DutyId = dutyId,
            GrantedDate = new DateOnly(2026, 1, 1),
            GrantedBy = adminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        Db.DutyRules.Add(new DutyRule
        {
            DutyId = dutyId,
            TierOrder = 1,
            CriteriaType = "BranchAttendanceRecency",
            CriteriaValue = "30",
        });
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        await service.RunAsync(new GenerateAssignmentCommand
        {
            OccurrenceId = occurrenceId,
        });

        var assignments = await Db.RosterAssignments
            .Where(a => a.MemberId == memberId)
            .CountAsync();

        assignments.Should().Be(1,
            "attendance at the specified branch must satisfy the criterion (5.0 doc line 97)");
    }

    // -----------------------------------------------------------------
    // Item 3 — NotificationChannel fallback
    // -----------------------------------------------------------------

    // Matrix row 9.0:99deb4ded8ce: NotificationChannel absent and Required = true | Dispatch is attempted
    [Fact]
    [Trait("MatrixRow", "9.0:99deb4ded8ce")]
    public async Task Notification_WithRequiredChannelAbsent_MustNotDispatch()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        // Required setting, absent. §14.1.2 + 9.0 doc line 70:
        // 9.0 refuses to send and surfaces a configuration error.
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "NotificationChannel",
            Value = null,
            Required = true,
        });
        await Db.SaveChangesAsync();

        var notification = new FakeDispatchNotificationService();
        var service = new MknMs.Application.Processes.Operations.DispatchNotification
            .DispatchNotificationService(Db, new CapturingLogger<MknMs.Application.Processes.Operations.DispatchNotification.DispatchNotificationService>());

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
        Db.RosterAssignments.Add(assignment);
        await Db.SaveChangesAsync();

        var act = async () => await service.DispatchAssignmentNoticeAsync(assignment);

        await act.Should().ThrowAsync<InvalidOperationException>(
            "a required NotificationChannel that is absent must surface a configuration error (9.0 doc line 70)");
    }

    // Matrix row 9.0:63893ab2ecd9: NotificationChannel absent and Required = false | Dispatch is attempted
    [Fact]
    [Trait("MatrixRow", "9.0:63893ab2ecd9")]
    public async Task Notification_WithOptionalChannelAbsent_MustNotLog()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        // Optional setting, absent. §14.1.2 + 9.0 doc line 70:
        // no notification is sent.
        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "NotificationChannel",
            Value = null,
            Required = false,
        });
        await Db.SaveChangesAsync();

        var logger = new CapturingLogger<MknMs.Application.Processes.Operations.DispatchNotification.DispatchNotificationService>();
        var service = new MknMs.Application.Processes.Operations.DispatchNotification
            .DispatchNotificationService(Db, logger);

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
        Db.RosterAssignments.Add(assignment);
        await Db.SaveChangesAsync();

        await service.DispatchAssignmentNoticeAsync(assignment);

        logger.Entries.Should().BeEmpty(
            "an optional NotificationChannel that is absent must not send and must not log a notification (9.0 doc line 70)");
    }

    // Matrix row 9.0:a52c7271998d: NotificationChannel set to an unrecognised value | Dispatch is attempted
    [Fact]
    [Trait("MatrixRow", "9.0:a52c7271998d")]
    public async Task Notification_WithInvalidChannel_MustNotLog()
    {
        var (occurrenceId, dutyId, memberId, adminId, _) = await SeedBasicAsync();

        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "NotificationChannel",
            Value = "Not/AChannel",
            Required = false,
        });
        await Db.SaveChangesAsync();

        var logger = new CapturingLogger<MknMs.Application.Processes.Operations.DispatchNotification.DispatchNotificationService>();
        var service = new MknMs.Application.Processes.Operations.DispatchNotification
            .DispatchNotificationService(Db, logger);

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
        Db.RosterAssignments.Add(assignment);
        await Db.SaveChangesAsync();

        await service.DispatchAssignmentNoticeAsync(assignment);

        logger.Entries.Should().BeEmpty(
            "an invalid NotificationChannel must not silently log (9.0 doc line 66)");
    }
}
