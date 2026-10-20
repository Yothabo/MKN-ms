using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Application.Processes.Operations.GenerateAssignment;
using MknMs.Application.Processes.Operations.ManageConfirmation;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D6_Eligibility;
using MknMs.Domain.D7_RosterAssignment;
using MknMs.Persistence;
using MknMs.Application.Common;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace MknMs.IntegrationTests;

/// <summary>
/// Regression tests for three defects found by code review and proven
/// by running against the pre-fix code. Each test now asserts the
/// correct behaviour; each fails if the defect is reintroduced.
///
///   - 5.0 must not re-offer a slot to a member who already holds a
///     row on that (duty, occurrence) slot. The unique constraint in
///     §15.2.1 makes a second row impossible; before the fix, the
///     attempt crashed the whole run.
///   - 7.0 must not allow a terminal-to-non-terminal transition that
///     would push the slot above its required count (§11.3). Before
///     the fix, confirming a declined, already-replaced assignment
///     left the slot over capacity.
///   - 7.0's sweep must not create a replacement for a past-dated
///     occurrence. The transition is recorded, but no notice and no
///     replacement are produced for a service that has already
///     happened. This is a date-level guard; it adds no setting.
///
/// Runs against mkn_test.
///
/// Specification: §10.1.2, §11.1.3, §11.1.6, §11.1.7, §11.3, §15.2.1.
/// </remarks>
public class FailingDefectTests : IntegrationTestBase
{
    private static readonly DateTimeOffset Now = new(2026, 10, 6, 12, 0, 0, TimeSpan.Zero);

    private readonly IDispatchNotificationService _notification = new FakeDispatchNotificationService();
    private readonly TimeProvider _clock = new FakeTimeProvider(Now);

    protected override async Task OnInitializedAsync()
    {
        // No migration seeds SystemSetting. These tests depend on
        // ApplicationTimeZone and AttendanceRegisterEnabled being
        // present. Seed them per fact. The helper is idempotent.
        await TestSeed.EnsureRequiredSettingsAsync(Db);
    }

    private sealed record Fixture(
        int OccurrenceId,
        int DutyId,
        int BeatriceId,
        int DanaId,
        int BeatriceAssignmentId,
        int ConfirmedStatusId,
        int DeclinedStatusId,
        int TimedOutStatusId);

    private async Task<Fixture> SeedAsync(
        DateOnly occurrenceDate,
        bool danaEligible,
        TimeSpan? assignmentAge = null)
    {
        Db.ServiceTypes.Add(new ServiceType { Name = "Regular" });
        Db.TimeOfDays.Add(new TimeOfDay { Name = "TestSlot" });

        var role = new Role { Name = "Member", IsActive = true };
        Db.Roles.Add(role);

        var duty = new Duty { Name = "Test duty", IsActive = true };
        Db.Duties.Add(duty);

        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        Db.Branches.Add(branch);

        var tier = new PermissionTier { Name = "Full Admin" };
        Db.PermissionTiers.Add(tier);

        var confirmed = new AssignmentStatus { Name = "Confirmed", IsTerminal = false };
        var declined = new AssignmentStatus { Name = "Declined", IsTerminal = true };
        var timedOut = new AssignmentStatus { Name = "Timed Out", IsTerminal = true };
        Db.AssignmentStatuses.AddRange(confirmed, declined, timedOut);
        await Db.SaveChangesAsync();

        Db.SystemSettings.AddRange(
            new SystemSetting { Key = "InitialAssignmentStatusID", Value = confirmed.AssignmentStatusId.ToString(), Required = true },
            new SystemSetting { Key = "DeclinedStatusID", Value = declined.AssignmentStatusId.ToString(), Required = true },
            new SystemSetting { Key = "TimedOutStatusID", Value = timedOut.AssignmentStatusId.ToString(), Required = true },
            new SystemSetting { Key = "ConfirmationTimeoutHours", Value = "48", Required = true });
        await Db.SaveChangesAsync();

        var memberStatusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        Member NewMember(string name, string phone) => new()
        {
            Name = name, Surname = "Test",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = phone, Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = memberStatusId,
        };

        var alex = NewMember("Alex", "+27-000-0000");
        var beatrice = NewMember("Beatrice", "+27-000-0001");
        var dana = NewMember("Dana", "+27-000-0002");
        Db.Members.AddRange(alex, beatrice, dana);
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

        Eligibility Grant(int memberId) => new()
        {
            MemberId = memberId,
            DutyId = duty.DutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = admin.AdminId,
            RevokedDate = null,
            RevokedReason = null,
        };

        Db.Eligibilities.Add(Grant(beatrice.MemberId));
        if (danaEligible) Db.Eligibilities.Add(Grant(dana.MemberId));

        var occurrence = new ServiceOccurrence
        {
            ScheduleId = schedule.ScheduleId,
            EventId = null,
            Date = occurrenceDate,
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(occurrence);
        await Db.SaveChangesAsync();

        var assignment = new RosterAssignment
        {
            MemberId = beatrice.MemberId,
            DutyId = duty.DutyId,
            OccurrenceId = occurrence.OccurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = Now - (assignmentAge ?? TimeSpan.Zero),
        };
        Db.RosterAssignments.Add(assignment);
        await Db.SaveChangesAsync();

        return new Fixture(
            occurrence.OccurrenceId, duty.DutyId,
            beatrice.MemberId, dana.MemberId, assignment.AssignmentId,
            confirmed.AssignmentStatusId, declined.AssignmentStatusId, timedOut.AssignmentStatusId);
    }

    // -----------------------------------------------------------------
    // Defect 1 — 5.0 must not re-offer a slot to a decliner
    // -----------------------------------------------------------------

    [Fact]
    public async Task Defect_5_0_MustNotReofferSlotToDecliner()
    {
        // Beatrice is the only eligible member and already holds a
        // declined row on the slot. 5.0 runs against the same
        // occurrence. It must not offer the slot again — the unique
        // index forbids a second row — and the run must succeed with
        // zero assignments created.
        var f = await SeedAsync(new DateOnly(2026, 10, 11), danaEligible: false);

        var declined = await Db.RosterAssignments.FirstAsync(a => a.AssignmentId == f.BeatriceAssignmentId);
        declined.AssignmentStatusId = f.DeclinedStatusId;
        await Db.SaveChangesAsync();

        var service = new GenerateAssignmentService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<GenerateAssignmentService>.Instance);
        var result = await service.RunAsync(new GenerateAssignmentCommand { OccurrenceId = f.OccurrenceId });

        result.Status.Should().Be("Success", result.ErrorDetail ?? "");
        result.AssignmentsCreated.Should().Be(0);
        result.DutiesUnfilled.Should().Be(1);

        var total = await Db.RosterAssignments.CountAsync();
        total.Should().Be(1, "only the original declined row should exist");
    }

    // -----------------------------------------------------------------
    // Defect 2 — 7.0 must reject a terminal-to-non-terminal transition
    // that would overfill the slot
    // -----------------------------------------------------------------

    [Fact]
    public async Task Defect_7_0_MustRejectConfirmAfterDeclineThatWouldOverfill()
    {
        var f = await SeedAsync(new DateOnly(2026, 10, 11), danaEligible: true);

        var confirmation = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        // Beatrice declines; the service replaces her with Dana.
        var declineResult = await confirmation.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = f.BeatriceAssignmentId,
        });

        declineResult.Status.Should().Be("Success");
        declineResult.ReplacementsCreated.Should().Be(1);

        // Beatrice's assignment is confirmed. §11.3 forbids the slot
        // from ending with more than one live assignment, so the
        // transition must be rejected.
        var confirmResult = await confirmation.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Confirm,
            AssignmentId = f.BeatriceAssignmentId,
        });

        confirmResult.Status.Should().Be("Failure");
        confirmResult.ErrorDetail.Should().Contain("already filled");

        var liveCount = await Db.RosterAssignments
            .AsNoTracking()
            .CountAsync(a => a.OccurrenceId == f.OccurrenceId
                && a.DutyId == f.DutyId
                && (a.AssignmentStatusId == null || !a.AssignmentStatus!.IsTerminal));

        liveCount.Should().Be(1, "the slot must not exceed its required count");
    }

    // -----------------------------------------------------------------
    // Defect 3 — 7.0 sweep must not refill a past-dated occurrence
    // -----------------------------------------------------------------

    [Fact]
    public async Task Defect_7_0_MustNotRefillPastDatedOccurrence()
    {
        // Occurrence is the day before the fixed clock's "today".
        var f = await SeedAsync(
            new DateOnly(2026, 10, 5),
            danaEligible: true,
            assignmentAge: TimeSpan.FromHours(72));

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Sweep,
            AssignmentId = null,
        });

        result.Status.Should().Be("Success", result.ErrorDetail ?? "");

        // The transition is recorded — the member did not respond.
        result.AssignmentsTransitioned.Should().Be(1);

        // But no replacement is sought for a service that has already
        // happened, and no notice goes out.
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(0);

        var rows = await Db.RosterAssignments.AsNoTracking().ToListAsync();
        rows.Should().HaveCount(1);
        rows.Single().AssignmentStatusId.Should().Be(f.TimedOutStatusId);

        var fake = (FakeDispatchNotificationService)_notification;
        fake.DispatchedAssignmentIds.Should().BeEmpty();
    }
}
