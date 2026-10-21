using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using MknMs.Application.Processes.Operations.EvaluateFillStatus;
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
/// Integration tests for process 10.0 Evaluate Fill Status.
/// </summary>
public class EvaluateFillStatusTests : IntegrationTestBase
{
    protected override async Task OnInitializedAsync()
    {
        // 10.0 requires ApplicationTimeZone and AttendanceRegisterEnabled
        // indirectly (via downstream processes it invokes during tests)
        // and directly relies on the required settings for its own checks.
        // No migration seeds them; seed per fact. The helper is idempotent.
        await TestSeed.EnsureRequiredSettingsAsync(Db);
    }

    // -----------------------------------------------------------------
    // Fixture
    // -----------------------------------------------------------------

    private sealed record Fixture(
        int OccurrenceId,
        int DutyId,
        int UnfilledStateId,
        int PartiallyFilledStateId,
        int FilledStateId,
        int CancelledStateId,
        int NonTerminalStatusId,
        int TerminalStatusId,
        int MemberOneId,
        int MemberTwoId);

    private async Task<Fixture> SeedFixtureAsync(
        bool includeRequiredSettings = true,
        bool includeCancelledSetting = false,
        bool includeRequiredDuty = true)
    {
        var unfilled = new OutcomeState { Name = "Unfilled" };
        var partially = new OutcomeState { Name = "Partially Filled" };
        var filled = new OutcomeState { Name = "Filled" };
        var cancelled = new OutcomeState { Name = "Cancelled" };
        Db.OutcomeStates.AddRange(unfilled, partially, filled, cancelled);

        var serviceType = new ServiceType { Name = "Regular" };
        Db.ServiceTypes.Add(serviceType);

        var timeOfDay = new TimeOfDay { Name = "Morning" };
        Db.TimeOfDays.Add(timeOfDay);

        var role = new Role { Name = "Coordinator", IsActive = true };
        Db.Roles.Add(role);

        var duty = new Duty { Name = "Reading duty", IsActive = true };
        Db.Duties.Add(duty);

        var branch = new Branch { Name = "Main", Location = "Main Street", IsActive = true };
        Db.Branches.Add(branch);

        var proposed = new AssignmentStatus { Name = "Proposed", IsTerminal = false };
        var declined = new AssignmentStatus { Name = "Declined", IsTerminal = true };
        Db.AssignmentStatuses.Add(proposed);
        Db.AssignmentStatuses.Add(declined);

        await Db.SaveChangesAsync();

        if (includeRequiredSettings)
        {
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStateUnfilledID",
                Value = unfilled.OutcomeStateId.ToString(),
                Required = true,
            });
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStatePartiallyFilledID",
                Value = partially.OutcomeStateId.ToString(),
                Required = true,
            });
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStateFilledID",
                Value = filled.OutcomeStateId.ToString(),
                Required = true,
            });
        }

        if (includeCancelledSetting)
        {
            Db.SystemSettings.Add(new SystemSetting
            {
                Key = "OutcomeStateCancelledID",
                Value = cancelled.OutcomeStateId.ToString(),
                Required = false,
            });
        }

        var statusId = await TestSeed.EnsureDefaultMemberStatusAsync(Db);

        var m1 = new Member
        {
            Name = "Alex", Surname = "Example",
            JoinDate = new DateOnly(2024, 1, 15),
            DateOfBirth = new DateOnly(1990, 5, 20),
            MembershipStage = "Full", Gender = "Other",
            Phone = "+27-000-0000", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        var m2 = new Member
        {
            Name = "Beatrice", Surname = "Sample",
            JoinDate = new DateOnly(2024, 6, 1),
            DateOfBirth = new DateOnly(1995, 3, 12),
            MembershipStage = "Full", Gender = "Female",
            Phone = "+27-000-0001", Email = null,
            BranchId = branch.BranchId, RoleId = role.RoleId,
            MemberStatusId = statusId,
        };
        Db.Members.Add(m1);
        Db.Members.Add(m2);
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

        // One duty, two required slots. This lets us exercise Unfilled
        // (0 active), Partially Filled (1 active), Filled (2 active).
        // The zero-duty case skips the row.
        if (includeRequiredDuty)
        {
            Db.ServiceDefinitionDuties.Add(new ServiceDefinitionDuty
            {
                ServiceDefId = definition.ServiceDefId,
                DutyId = duty.DutyId,
                RequiredSlotCount = 2,
            });
            await Db.SaveChangesAsync();
        }

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

        return new Fixture(
            occurrence.OccurrenceId,
            duty.DutyId,
            unfilled.OutcomeStateId,
            partially.OutcomeStateId,
            filled.OutcomeStateId,
            cancelled.OutcomeStateId,
            proposed.AssignmentStatusId,
            declined.AssignmentStatusId,
            m1.MemberId,
            m2.MemberId);
    }

    private async Task AddAssignmentAsync(
        Fixture f,
        int memberId,
        int? statusId)
    {
        Db.RosterAssignments.Add(new RosterAssignment
        {
            MemberId = memberId,
            DutyId = f.DutyId,
            OccurrenceId = f.OccurrenceId,
            AssignmentStatusId = statusId,
            ApprovedBy = null,
            AssignmentSource = "Automatic",
            AssignedBy = null,
            CreatedAt = DateTimeOffset.UtcNow,
        });
        await Db.SaveChangesAsync();
    }

    // -----------------------------------------------------------------
    // Tests
    // -----------------------------------------------------------------

    // Matrix row 10.0:d83acc9018b7: An occurrence with a required count of 2 and 0 active assignments | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:d83acc9018b7")]
    public async Task Evaluate_WithZeroActiveAssignments_WritesUnfilled()
    {
        var f = await SeedFixtureAsync();
        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(1);
        result.UnfilledCount.Should().Be(1);

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.UnfilledStateId);
    }

    // Matrix row 10.0:8427b5f05879: An occurrence with a required count of 2 and 1 active assignment | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:8427b5f05879")]
    public async Task Evaluate_WithFewerActiveThanRequired_WritesPartiallyFilled()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.NonTerminalStatusId);

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.PartiallyFilledCount.Should().Be(1);

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.PartiallyFilledStateId);
    }

    // Matrix row 10.0:cea53becdc37: An occurrence with a required count of 2 and 2 active assignments | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:cea53becdc37")]
    public async Task Evaluate_WithActiveMeetRequired_WritesFilled()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.NonTerminalStatusId);
        await AddAssignmentAsync(f, f.MemberTwoId, f.NonTerminalStatusId);

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.FilledCount.Should().Be(1);

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.FilledStateId);
    }

    // Matrix row 10.0:695829c8295f: An occurrence with all terminal assignments and a required count > 0 | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:695829c8295f")]
    public async Task Evaluate_WithTerminalAssignments_DoesNotCountThem()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.TerminalStatusId);
        await AddAssignmentAsync(f, f.MemberTwoId, f.TerminalStatusId);

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.UnfilledCount.Should().Be(1);

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.UnfilledStateId);
    }

    // Matrix row 10.0:a0f479392fba: An occurrence with a required count of 0 | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:a0f479392fba")]
    public async Task Evaluate_WithZeroRequiredDuties_ClassifiesAsFilled()
    {
        // Zero required duties. The effective required count is zero.
        // The zero-duty rule (§10.1.2, §12.1.2) classifies the
        // occurrence as Filled, because active (0) meets required (0).
        var f = await SeedFixtureAsync(includeRequiredDuty: false);
        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.FilledCount.Should().Be(1);

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().Be(f.FilledStateId);
    }

    // Matrix row 10.0:0bed694007ba: An occurrence whose current FillStatusID equals OutcomeStateCancelledID | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:0bed694007ba")]
    public async Task Evaluate_WithCancelledCurrentStatus_Skips()
    {
        var f = await SeedFixtureAsync(includeCancelledSetting: true);

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId = f.CancelledStateId;
        await Db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(0);
        result.SkippedCancelledCount.Should().Be(1);

        var after = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        after.FillStatusId.Should().Be(f.CancelledStateId);
    }

    // Matrix row 10.0:9b65bcb2ab48: OutcomeStateCancelledID is absent | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:9b65bcb2ab48")]
    public async Task Evaluate_WithCancelledSettingAbsent_NoExclusion()
    {
        // No OutcomeStateCancelledID setting. The cancelled state is
        // not excluded; the occurrence is evaluated and the mechanical
        // classification overwrites its current FillStatusId.
        var f = await SeedFixtureAsync(includeCancelledSetting: false);

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId = f.CancelledStateId;
        await Db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(1);
        result.SkippedCancelledCount.Should().Be(0);
        result.UnfilledCount.Should().Be(1);

        var after = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        after.FillStatusId.Should().Be(f.UnfilledStateId);
    }

    // Matrix row 10.0:3620770fc1e4: Any of the three required mapping settings absent | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:3620770fc1e4")]
    public async Task Evaluate_WithoutRequiredSetting_Fails()
    {
        var f = await SeedFixtureAsync(includeRequiredSettings: false);
        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeState");

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_WithoutUnfilledSetting_Fails()
    {
        // Only OutcomeStateUnfilledID removed; the other two remain.
        // The whole run is refused, not just the affected occurrences.
        var f = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "OutcomeStateUnfilledID");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeStateUnfilledID");

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_WithoutPartiallyFilledSetting_Fails()
    {
        var f = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "OutcomeStatePartiallyFilledID");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeStatePartiallyFilledID");

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    [Fact]
    public async Task Evaluate_WithoutFilledSetting_Fails()
    {
        var f = await SeedFixtureAsync();

        var setting = await Db.SystemSettings
            .FirstAsync(s => s.Key == "OutcomeStateFilledID");
        Db.SystemSettings.Remove(setting);
        await Db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Failure");
        result.OccurrencesEvaluated.Should().Be(0);
        result.ErrorDetail.Should().Contain("OutcomeStateFilledID");

        var occ = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);
        occ.FillStatusId.Should().BeNull();
    }

    // Matrix row 10.0:527bea74d24f: An occurrence already evaluated with no state change | 10.0 runs again
    [Fact]
    [Trait("MatrixRow", "10.0:527bea74d24f")]
    public async Task Evaluate_IsIdempotent()
    {
        var f = await SeedFixtureAsync();
        await AddAssignmentAsync(f, f.MemberOneId, f.NonTerminalStatusId);

        var service = new EvaluateFillStatusService(Db);

        await service.RunAsync(new EvaluateFillStatusCommand { OccurrenceId = f.OccurrenceId });
        var first = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);

        await service.RunAsync(new EvaluateFillStatusCommand { OccurrenceId = f.OccurrenceId });
        var second = await Db.ServiceOccurrences.FirstAsync(o => o.OccurrenceId == f.OccurrenceId);

        second.FillStatusId.Should().Be(first.FillStatusId);
    }

    // Matrix row 10.0:62e0362c9a71: A single-occurrence invocation | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:62e0362c9a71")]
    public async Task Evaluate_SingleOccurrence_DoesNotTouchOtherOccurrences()
    {
        // A single-occurrence invocation must evaluate only that
        // occurrence. A second occurrence with the same required duty
        // is left untouched.
        var f = await SeedFixtureAsync();

        var scheduleId = await Db.ServiceOccurrences
            .Where(o => o.OccurrenceId == f.OccurrenceId)
            .Select(o => o.ScheduleId!.Value)
            .FirstAsync();

        var otherOccurrence = new ServiceOccurrence
        {
            ScheduleId = scheduleId,
            EventId = null,
            Date = new DateOnly(2026, 10, 18),
            ServiceTypeId = null,
            StartTime = null,
            FillStatusId = null,
            GeneratedBy = "System",
            CreatedBy = null,
            ChangedBy = null,
        };
        Db.ServiceOccurrences.Add(otherOccurrence);
        await Db.SaveChangesAsync();

        var service = new EvaluateFillStatusService(Db);
        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
        });

        result.Status.Should().Be("Success");
        result.OccurrencesEvaluated.Should().Be(1);

        var untouched = await Db.ServiceOccurrences
            .FirstAsync(o => o.OccurrenceId == otherOccurrence.OccurrenceId);
        untouched.FillStatusId.Should().BeNull();
    }

    // Matrix row 10.0:2d96f46193cb: Neither occurrence nor range supplied | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:2d96f46193cb")]
    public async Task Run_WithInvalidCommand_Fails()
    {
        await SeedFixtureAsync();
        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand());

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Exactly one");
    }

    // Matrix row 10.0:e56a0e982ec5: Both a single occurrence and a range supplied | 10.0 runs
    [Fact]
    [Trait("MatrixRow", "10.0:e56a0e982ec5")]
    public async Task Run_WithBothOccurrenceAndRange_Fails()
    {
        var f = await SeedFixtureAsync();
        var service = new EvaluateFillStatusService(Db);

        var result = await service.RunAsync(new EvaluateFillStatusCommand
        {
            OccurrenceId = f.OccurrenceId,
            FromDate = new DateOnly(2026, 10, 1),
            ToDate = new DateOnly(2026, 10, 31),
        });

        result.Status.Should().Be("Failure");
        result.ErrorDetail.Should().Contain("Exactly one");
    }
}
