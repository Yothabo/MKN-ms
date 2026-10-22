using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Common;
using MknMs.Application.Processes.Operations.DispatchNotification;
using MknMs.Application.Processes.Operations.ManageConfirmation;
using MknMs.Domain.D10_ConfigLookups;
using MknMs.Domain.D11_SystemSetting;
using MknMs.Domain.D1_RoleDuty;
using MknMs.Domain.D2_DutyRule;
using MknMs.Domain.D3_BranchTimeSlotService;
using MknMs.Domain.D4_Member;
using MknMs.Domain.D6_Eligibility;
using MknMs.Domain.D7_RosterAssignment;
using MknMs.Persistence;
using Xunit;
using Microsoft.Extensions.Logging.Abstractions;

namespace MknMs.IntegrationTests;

/// <summary>
/// Integration tests for process 7.0 Manage Confirmation.
/// </summary>
/// <remarks>
/// Runs against mkn_test. The service reads the target status from
/// SystemSetting based on the operation:
///
///   - Confirm reads InitialAssignmentStatusID
///   - Decline reads DeclinedStatusID
///   - Sweep reads TimedOutStatusID
///
/// The fixture seeds all three settings so each operation resolves.
///
/// Reference: docs/processes/operations/7.0-manage-confirmation.md,
/// Specification §11.
/// </remarks>
public class ManageConfirmationTests : IntegrationTestBase
{
    private readonly IDispatchNotificationService _notification = new FakeDispatchNotificationService();
    private readonly TimeProvider _clock = new FakeTimeProvider(
        new DateTimeOffset(2026, 10, 6, 12, 0, 0, TimeSpan.Zero));

    protected override async Task OnInitializedAsync()
    {
        await TestSeed.EnsureRequiredSettingsAsync(Db);
    }

    private sealed record Fixture(
        int OccurrenceId,
        int ReadingDutyId,
        int AssignmentId,
        int BeatriceMemberId,
        int DanaMemberId,
        int ProposedStatusId,
        int ConfirmedStatusId,
        int DeclinedStatusId,
        int TimedOutStatusId);

    private async Task<Fixture> SeedFixtureAsync(
        bool includeInitialStatusSetting = true,
        bool includeTimeoutSetting = true)
    {
        Db.OutcomeStates.Add(new OutcomeState { Name = "Unfilled" });
        Db.ServiceTypes.Add(new ServiceType { Name = "Regular" });
        Db.TimeOfDays.Add(new TimeOfDay { Name = "Morning" });

        var coordinator = new Role { Name = "Coordinator", IsActive = true };
        var steward = new Role { Name = "Steward", IsActive = true };
        Db.Roles.Add(coordinator);
        Db.Roles.Add(steward);

        var welcomeDuty = new Duty { Name = "Welcome duty", IsActive = true };
        var readingDuty = new Duty { Name = "Reading duty", IsActive = true };
        Db.Duties.Add(welcomeDuty);
        Db.Duties.Add(readingDuty);

        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        Db.Branches.Add(branch);

        var tier = new PermissionTier { Name = "Full Admin" };
        Db.PermissionTiers.Add(tier);

        var proposed = new AssignmentStatus { Name = "Proposed", IsTerminal = false };
        var confirmed = new AssignmentStatus { Name = "Confirmed", IsTerminal = false };
        var declined = new AssignmentStatus { Name = "Declined", IsTerminal = true };
        var timedOut = new AssignmentStatus { Name = "Timed Out", IsTerminal = true };
        Db.AssignmentStatuses.Add(proposed);
        Db.AssignmentStatuses.Add(confirmed);
        Db.AssignmentStatuses.Add(declined);
        Db.AssignmentStatuses.Add(timedOut);

        await Db.SaveChangesAsync();

        if (includeInitialStatusSetting)
        {
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "InitialAssignmentStatusID",
                Value = confirmed.AssignmentStatusId.ToString(),
                Required = true,
            });
        }

        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "DeclinedStatusID",
            Value = declined.AssignmentStatusId.ToString(),
            Required = true,
        });

        Db.SystemSettings.Add(new SystemSetting
        {
            Key = "TimedOutStatusID",
            Value = timedOut.AssignmentStatusId.ToString(),
            Required = true,
        });

        if (includeTimeoutSetting)
        {
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "ConfirmationTimeoutHours",
                Value = "48",
                Required = true,
            });
        }

        await Db.SaveChangesAsync();

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var alex = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = coordinator.RoleId,
            MemberStatusId = statusId,
        };
        var beatrice = new Member
        {
            Name = "Beatrice", Surname = "Sample",
            JoinDate = new DateOnly(2024, 6, 1),
            DateOfBirth = new DateOnly(1995, 3, 12),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0001", Email = null,
            BranchId = branch.BranchId, RoleId = steward.RoleId,
            MemberStatusId = statusId,
        };
        var dana = new Member
        {
            Name = "Dana", Surname = "Prototype",
            JoinDate = new DateOnly(2023, 4, 20),
            DateOfBirth = new DateOnly(1985, 11, 30),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0003", Email = null,
            BranchId = branch.BranchId, RoleId = steward.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(alex);
        Db.Members.Add(beatrice);
        Db.Members.Add(dana);
        await Db.SaveChangesAsync();

        var admin = new Admin
        {
            MemberId = alex.MemberId,
            PermissionTierId = tier.PermissionTierId,
        };
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
            Name = "Sunday Service",
            ServiceTypeId = Db.ServiceTypes.Local.First().ServiceTypeId,
            OwningBranchId = null,
        };
        Db.ServiceDefinitions.Add(definition);
        await Db.SaveChangesAsync();

        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = welcomeDuty.DutyId,
            RequiredSlotCount = 1,
        });
        Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
        {
            ServiceDefId = definition.ServiceDefId,
            DutyId = readingDuty.DutyId,
            RequiredSlotCount = 1,
        });
        await Db.SaveChangesAsync();

        var schedule = new ServiceSchedule
        {
            ServiceDefId = definition.ServiceDefId,
            TimeSlotId = slot.TimeSlotId,
            StartTime = new TimeOnly(9, 0),
        };
        Db.ServiceSchedules.Add(schedule);
        await Db.SaveChangesAsync();

        Db.DutyRules.Add(new DutyRule
        {
            DutyId = readingDuty.DutyId,
            TierOrder = 1,
            CriteriaType = "Role",
            CriteriaValue = "Steward",
        });
        await Db.SaveChangesAsync();

        Db.Eligibilities.Add(new Eligibility
        {
            MemberId = beatrice.MemberId,
            DutyId = readingDuty.DutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = admin.AdminId,
            RevokedDate = null,
            RevokedReason = null,
        });
        Db.Eligibilities.Add(new Eligibility
        {
            MemberId = dana.MemberId,
            DutyId = readingDuty.DutyId,
            GrantedDate = new DateOnly(2025, 1, 1),
            GrantedBy = admin.AdminId,
            RevokedDate = null,
            RevokedReason = null,
        });
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

        var assignment = new RosterAssignment
        {
            MemberId = beatrice.MemberId,
            DutyId = readingDuty.DutyId,
            OccurrenceId = occurrence.OccurrenceId,
            AssignmentStatusId = null,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        };
        Db.RosterAssignments.Add(assignment);
        await Db.SaveChangesAsync();

        return new Fixture(
            occurrence.OccurrenceId,
            readingDuty.DutyId,
            assignment.AssignmentId,
            beatrice.MemberId,
            dana.MemberId,
            proposed.AssignmentStatusId,
            confirmed.AssignmentStatusId,
            declined.AssignmentStatusId,
            timedOut.AssignmentStatusId);
    }

    // -----------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------

    // Matrix row 7.0:dc0451f28ec9: An assignment with `AssignmentStatusID = NULL` | Confirm is received
    [Fact]
    [Trait("MatrixRow", "7.0:dc0451f28ec9")]
    public async Task Confirm_TransitionsToInitialStatusWithoutReplacement()
    {
        var fixture = await SeedFixtureAsync();
        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Confirm,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(0);

        var assignment = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.AssignmentStatusId.Should().Be(fixture.ConfirmedStatusId);

        var totalAssignments = await Db.RosterAssignments.CountAsync();
        totalAssignments.Should().Be(1);
    }

    // Matrix row 7.0:38e640cd73bd: A decline with a terminal status and one candidate available | Decline is processed
    [Fact]
    [Trait("MatrixRow", "7.0:38e640cd73bd")]
    [Trait("MatrixRow", "7.0:31a44cddfd48")]
    public async Task Decline_WithTerminalStatus_AndCandidateAvailable_CreatesReplacement()
    {
        var fixture = await SeedFixtureAsync();
        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(1);
        result.SlotsLeftVacant.Should().Be(0);

        var assignments = await Db.RosterAssignments
            .OrderBy(a => a.AssignmentId)
            .ToListAsync();

        assignments.Should().HaveCount(2);

        var original = assignments[0];
        original.AssignmentId.Should().Be(fixture.AssignmentId);
        original.AssignmentStatusId.Should().Be(fixture.DeclinedStatusId);

        var replacement = assignments[1];
        replacement.MemberId.Should().Be(fixture.DanaMemberId);
        replacement.DutyId.Should().Be(fixture.ReadingDutyId);
        replacement.OccurrenceId.Should().Be(fixture.OccurrenceId);
        replacement.AssignmentStatusId.Should().BeNull();
        replacement.AssignmentSource.Should().Be("Automatic");
        replacement.AssignedBy.Should().BeNull();
        replacement.ApprovedBy.Should().BeNull();
    }

    // Matrix row 7.0:1bd5a14a7ee9: A decline with a terminal status and no candidate available | Decline is processed
    [Fact]
    [Trait("MatrixRow", "7.0:1bd5a14a7ee9")]
    public async Task Decline_WithTerminalStatus_AndNoCandidate_SlotLeftVacant()
    {
        var fixture = await SeedFixtureAsync();

        var danaEligibility = await Db.Eligibilities
            .Where(e => e.MemberId == fixture.DanaMemberId)
            .ToListAsync();
        Db.Eligibilities.RemoveRange(danaEligibility);
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(1);

        var totalAssignments = await Db.RosterAssignments.CountAsync();
        totalAssignments.Should().Be(1);
    }

    // Matrix row 7.0:bad2159cf30e: A member who previously declined the slot | Decline is processed again
    [Fact]
    [Trait("MatrixRow", "7.0:bad2159cf30e")]
    public async Task Decline_WithTerminalStatus_ExcludesMemberAlreadyTerminalOnThisSlot()
    {
        var fixture = await SeedFixtureAsync();

        Db.RosterAssignments.Add(new RosterAssignment
        {
            MemberId = fixture.DanaMemberId,
            DutyId = fixture.ReadingDutyId,
            OccurrenceId = fixture.OccurrenceId,
            AssignmentStatusId = fixture.DeclinedStatusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        });
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(1);

        var totalAssignments = await Db.RosterAssignments.CountAsync();
        totalAssignments.Should().Be(2);
    }

    // Matrix row 7.0:c7e8c401e964: An assignment with `AssignmentStatusID = NULL` past the timeout | The sweep runs
    [Fact]
    [Trait("MatrixRow", "7.0:c7e8c401e964")]
    public async Task Sweep_WithTimedOutAssignment_TransitionsAndReplaces()
    {
        var fixture = await SeedFixtureAsync();

        var assignment = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.CreatedAt = _clock.GetUtcNow().AddHours(-72);
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Sweep,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(1);

        var original = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        original.AssignmentStatusId.Should().Be(fixture.TimedOutStatusId);
    }

    // Matrix row 7.0:70d1d7b08ed6: An assignment whose elapsed age is below `ConfirmationTimeoutHours` | The sweep runs
    [Fact]
    [Trait("MatrixRow", "7.0:70d1d7b08ed6")]
    public async Task Sweep_WithAssignmentInsideWindow_DoesNotTransition()
    {
        var fixture = await SeedFixtureAsync();
        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Sweep,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(0);
        result.ReplacementsCreated.Should().Be(0);

        var assignment = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.AssignmentStatusId.Should().BeNull();
    }

    // Matrix row 7.0:b52cc205ea92: `InitialAssignmentStatusID` is absent | Confirm is requested
    [Fact]
    [Trait("MatrixRow", "7.0:b52cc205ea92")]
    public async Task Confirm_WithoutInitialStatusSetting_Fails()
    {
        var fixture = await SeedFixtureAsync(includeInitialStatusSetting: false);
        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Confirm,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("InitialAssignmentStatusID");
    }

    // Matrix row 7.0:870bae674fa2: `DeclinedStatusID` is absent | Decline is requested
    [Fact]
    [Trait("MatrixRow", "7.0:870bae674fa2")]
    public async Task Decline_WithoutDeclinedStatusSetting_Fails()
    {
        var fixture = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "DeclinedStatusID");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("DeclinedStatusID");
    }

    [Fact]
    public async Task Run_WithInvalidCommand_Fails()
    {
        await SeedFixtureAsync();
        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Confirm,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("valid operation");
    }

    [Fact]
    public async Task Run_ReadsTargetStatusFromSetting_NotFromCommand()
    {
        var fixture = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "InitialAssignmentStatusID");
        setting.Value = fixture.ProposedStatusId.ToString();
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Confirm,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");

        var assignment = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.AssignmentStatusId.Should().Be(fixture.ProposedStatusId);
    }

    // Matrix row 7.0:28da3be2da15: A terminal assignment | A non-terminal transition is requested, and the slot has room
    [Fact]
    [Trait("MatrixRow", "7.0:28da3be2da15")]
    public async Task Confirm_TerminalToNonTerminalWithRoom_Succeeds()
    {
        var fixture = await SeedFixtureAsync();

        var assignment = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.AssignmentStatusId = fixture.DeclinedStatusId;
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Confirm,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);

        var after = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        after.AssignmentStatusId.Should().Be(fixture.ConfirmedStatusId);

        var liveCount = await Db.RosterAssignments
            .Where(a => a.OccurrenceId == fixture.OccurrenceId
                && a.DutyId == fixture.ReadingDutyId
                && (a.AssignmentStatusId == null
                    || !Db.AssignmentStatuses
                        .Where(s => s.AssignmentStatusId == a.AssignmentStatusId)
                        .Select(s => s.IsTerminal)
                        .First()))
            .CountAsync();

        liveCount.Should().Be(1);
    }

    // Matrix row 7.0:d5982a2b9b85: A terminal-to-terminal transition | The transition is applied
    [Fact]
    [Trait("MatrixRow", "7.0:d5982a2b9b85")]
    public async Task Confirm_TerminalToTerminal_NoCapacityCheck()
    {
        var fixture = await SeedFixtureAsync();

        var assignment = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.AssignmentStatusId = fixture.TimedOutStatusId;
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);

        var after = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        after.AssignmentStatusId.Should().Be(fixture.DeclinedStatusId);
    }

    // Matrix row 7.0:3cd8ecb2132a: A member who previously timed out on the slot | Decline is processed again
    [Fact]
    [Trait("MatrixRow", "7.0:3cd8ecb2132a")]
    public async Task Decline_ExcludesMemberWhoPreviouslyTimedOutOnThisSlot()
    {
        var fixture = await SeedFixtureAsync();

        Db.RosterAssignments.Add(new RosterAssignment
        {
            MemberId = fixture.DanaMemberId,
            DutyId = fixture.ReadingDutyId,
            OccurrenceId = fixture.OccurrenceId,
            AssignmentStatusId = fixture.TimedOutStatusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = _clock.GetUtcNow(),
        });
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(1);

        var danaTerminal = await Db.RosterAssignments
            .CountAsync(a => a.MemberId == fixture.DanaMemberId
                && a.DutyId == fixture.ReadingDutyId
                && a.OccurrenceId == fixture.OccurrenceId);
        danaTerminal.Should().Be(1);
    }

    // Matrix row 7.0:5d023cd4fe8b: `TimedOutStatusID` is absent | The sweep runs
    [Fact]
    [Trait("MatrixRow", "7.0:5d023cd4fe8b")]
    public async Task Sweep_WithoutTimedOutStatusSetting_Fails()
    {
        var fixture = await SeedFixtureAsync();

        var assignment = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        assignment.CreatedAt = _clock.GetUtcNow().AddHours(-72);
        await Db.SaveChangesAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "TimedOutStatusID");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Sweep,
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("TimedOutStatusID");

        var after = await Db.RosterAssignments
            .FirstAsync(a => a.AssignmentId == fixture.AssignmentId);
        after.AssignmentStatusId.Should().BeNull();
    }

    // Matrix row 7.0:3ecfc0933ba5: An assignment whose target status is non-terminal | Decline is processed
    [Fact]
    [Trait("MatrixRow", "7.0:3ecfc0933ba5")]
    public async Task Decline_WithNonTerminalTargetStatus_DoesNotReResolve()
    {
        var fixture = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "DeclinedStatusID");
        setting.Value = fixture.ProposedStatusId.ToString();
        await Db.SaveChangesAsync();

        var service = new ManageConfirmationService(Db, _notification, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.AssignmentsTransitioned.Should().Be(1);
        result.ReplacementsCreated.Should().Be(0);
        result.SlotsLeftVacant.Should().Be(0);

        var assignments = await Db.RosterAssignments.CountAsync();
        assignments.Should().Be(1);
    }

    // Matrix row 7.0:efc492b2c577: A replacement is created | The replacement's 9.0 invocation fails
    [Fact]
    [Trait("MatrixRow", "7.0:efc492b2c577")]
    public async Task Decline_ReplacementNotificationFails_ReplacementCommitted()
    {
        var fixture = await SeedFixtureAsync();

        var throwing = new ThrowingDispatchNotificationService();
        var service = new ManageConfirmationService(Db, throwing, _clock, new OccurrenceLockService(Db), NullLogger<ManageConfirmationService>.Instance);

        var result = await service.RunAsync(new ManageConfirmationCommand
        {
            Operation = ManageConfirmationOperation.Decline,
            AssignmentId = fixture.AssignmentId,
        });

        result.Status.Should().Be("Success");
        result.ReplacementsCreated.Should().Be(1);

        var replacement = await Db.RosterAssignments
            .Where(a => a.AssignmentId != fixture.AssignmentId)
            .FirstAsync();
        replacement.MemberId.Should().Be(fixture.DanaMemberId);
        replacement.AssignmentStatusId.Should().BeNull();
    }
}
